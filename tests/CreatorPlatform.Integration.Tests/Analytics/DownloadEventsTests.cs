using CreatorPlatform.Access.Infrastructure.Services;
using CreatorPlatform.Analytics.Application.Series;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>
/// R6.2: every download through a personal link is an event, written with the link's counter in one statement (a
/// revoked link never counts), keeping the page the link came from; the series count them on the day they happen in
/// the creator's timezone, per product, per page and for the whole workspace.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DownloadEventsTests
{
    private const string Zagreb = "Europe/Zagreb";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public DownloadEventsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private sealed record Seeded(
        TestData.SeededCreator Creator, Guid FreeProduct, Guid PaidProduct, int LeadPage, Guid LeadPagePublicId,
        int SalesPage, Guid SalesPagePublicId, string SignupToken, string SalesToken, string DirectToken, string RefundedToken);

    private static DateTimeOffset LocalMidnight()
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).Date;
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }

    private static DateTimeOffset LocalNoon(int days)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).Date.AddDays(days).AddHours(12);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }

    private async Task<T> ScalarAsync<T>(FormattableString sql)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<T>(sql).ToListAsync()).Single();
    }

    private async Task<Seeded> SeedAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creator.CreatorId}, 'Brand', '#E2553A', {Zagreb}, 'en', now(), now())
            """);

        async Task<(int Id, Guid PublicId)> Product(int priceCents)
        {
            var publicId = Guid.NewGuid();
            var id = (await db.Database.SqlQuery<int>($"""
                INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName",
                                               "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
                VALUES ({publicId}, {creator.CreatorId}, 'Guide', {priceCents}, 'Active', 'ProductFile/test/guide.pdf', 'guide.pdf',
                        1024, 'application/pdf', now(), now() - interval '400 days', now())
                RETURNING "Id" AS "Value"
                """).ToListAsync()).Single();
            return (id, publicId);
        }

        async Task<(int Id, Guid PublicId)> Page(int productId, string type)
        {
            var publicId = Guid.NewGuid();
            var id = (await db.Database.SqlQuery<int>($"""
                INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
                VALUES ({publicId}, {creator.CreatorId}, {productId}, 'Page', {$"p-{Guid.NewGuid():N}"}, {type}, 'Published',
                        now() - interval '400 days', now())
                RETURNING "Id" AS "Value"
                """).ToListAsync()).Single();
            return (id, publicId);
        }

        async Task<string> OrderGrant(int productId, int? landingPageId, bool refunded)
        {
            var orderId = (await db.Database.SqlQuery<int>($"""
                INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "LandingPageId", "Email", "AmountCents", "Currency", "Status",
                                           "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                           "CreatedAt", "PaidAt", "UpdatedAt", "RefundedCents")
                VALUES ({Guid.NewGuid()}, {creator.CreatorId}, {productId}, {landingPageId}, {TestData.UniqueEmail("buyer")}, 2900, 'Eur',
                        {(refunded ? "Refunded" : "Paid")}, 0, 0, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"},
                        now() - interval '30 days', now() - interval '30 days', now(), {(refunded ? 2900 : 0)})
                RETURNING "Id" AS "Value"
                """).ToListAsync()).Single();
            var token = CreatorPlatform.Products.Domain.Access.AccessGrant.NewToken();
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO products.access_grants ("Token", "CreatorId", "ProductId", "OrderId", "Email", "CreatedAt", "RevokedAt", "DownloadCount")
                VALUES ({token}, {creator.CreatorId}, {productId}, {orderId}, 'buyer@example.test', now() - interval '30 days',
                        {(refunded ? DateTimeOffset.UtcNow.AddDays(-1) : null)}, 0)
                """);
            return token;
        }

        var free = await Product(0);
        var paid = await Product(2900);
        var lead = await Page(free.Id, "LeadGen");
        var sales = await Page(paid.Id, "Sales");

        var email = TestData.UniqueEmail("lead");
        await _data.CaptureAsync(creator.CreatorId, lead.Id, email);   // the production path creates the sign-up's link
        var signupToken = await ScalarAsync<string>($"""
            SELECT g."Token" AS "Value" FROM products.access_grants g
            JOIN analytics.email_captures ec ON ec."Id" = g."EmailCaptureId"
            WHERE ec."LandingPageId" = {lead.Id} AND ec."Email" = {email}
            """);

        return new Seeded(creator, free.PublicId, paid.PublicId, lead.Id, lead.PublicId, sales.Id, sales.PublicId,
            signupToken,
            await OrderGrant(paid.Id, sales.Id, refunded: false),
            await OrderGrant(paid.Id, null, refunded: false),
            await OrderGrant(paid.Id, sales.Id, refunded: true));
    }

    private async Task<bool> DownloadAsync(string token, DateTimeOffset at)
    {
        await using var db = _fixture.CreateDbContext();
        return await new AccessGrantReader(db).RecordDownloadAsync(token, at, None) is not null;
    }

    private async Task<AnalyticsSeriesDto> SeriesAsync(TestData.SeededCreator creator, string scope, Guid id, string period)
    {
        await using var db = _fixture.CreateDbContext();
        return await new AnalyticsSeriesService(new AnalyticsSeriesReader(db)).GetAsync(creator.Slug, creator.OwnerUserId, scope, id, period, None);
    }

    private sealed record EventRow(string Token, int? LandingPageId, DateTimeOffset DownloadedAt);

    private async Task<List<EventRow>> EventsAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<EventRow>($"""
            SELECT g."Token", de."LandingPageId", de."DownloadedAt"
            FROM products.download_events de JOIN products.access_grants g ON g."Id" = de."GrantId"
            WHERE de."CreatorId" = {creatorId}
            ORDER BY de."DownloadedAt"
            """).ToListAsync();
    }

    [Fact]
    public async Task EveryDownload_IsAnEvent_WithThePageTheLinkCameFrom_AndARevokedLinkNever()
    {
        var s = await SeedAsync();

        Assert.True(await DownloadAsync(s.SignupToken, LocalNoon(0)));
        Assert.True(await DownloadAsync(s.SignupToken, LocalNoon(0).AddMinutes(5)));   // a repeat counts too
        Assert.True(await DownloadAsync(s.SalesToken, LocalNoon(-1)));
        Assert.True(await DownloadAsync(s.DirectToken, LocalNoon(-2)));
        Assert.False(await DownloadAsync(s.RefundedToken, LocalNoon(0)));

        var events = await EventsAsync(s.Creator.CreatorId);
        Assert.Equal(4, events.Count);
        Assert.Equal([s.LeadPage, s.LeadPage], events.Where(e => e.Token == s.SignupToken).Select(e => e.LandingPageId));
        Assert.Equal(s.SalesPage, events.Single(e => e.Token == s.SalesToken).LandingPageId);
        Assert.Null(events.Single(e => e.Token == s.DirectToken).LandingPageId);   // an order placed without a page
        Assert.DoesNotContain(events, e => e.Token == s.RefundedToken);
        Assert.Equal(0, await ScalarAsync<int>($"""SELECT "DownloadCount" AS "Value" FROM products.access_grants WHERE "Token" = {s.RefundedToken}"""));
        Assert.Equal(2, await ScalarAsync<int>($"""SELECT "DownloadCount" AS "Value" FROM products.access_grants WHERE "Token" = {s.SignupToken}"""));
    }

    [Fact]
    public async Task TheSeries_CountDownloadsOnTheirLocalDay_PerProduct_PerPage_AndForTheWorkspace()
    {
        var s = await SeedAsync();
        var midnight = LocalMidnight();
        await DownloadAsync(s.SignupToken, midnight);                       // 00:00 in Zagreb: today (22:00/23:00 UTC yesterday)
        await DownloadAsync(s.SignupToken, midnight.AddMinutes(-1));        // 23:59 in Zagreb: yesterday
        await DownloadAsync(s.SignupToken, LocalNoon(-9));                  // the previous 7 days
        await DownloadAsync(s.SalesToken, LocalNoon(0));
        await DownloadAsync(s.DirectToken, LocalNoon(-3));

        var free = await SeriesAsync(s.Creator, "product", s.FreeProduct, "7d");
        Assert.Equal((2L, 1L), (free.Totals.Downloads, free.PreviousTotals!.Downloads));
        Assert.Equal(1L, free.Buckets[^1].Downloads);
        Assert.Equal(1L, free.Buckets[^2].Downloads);
        Assert.Equal(1L, free.PreviousBuckets!.Sum(b => b.Downloads));

        var paid = await SeriesAsync(s.Creator, "product", s.PaidProduct, "7d");
        Assert.Equal(2L, paid.Totals.Downloads);                            // paid-order downloads are counted too

        var leadPage = await SeriesAsync(s.Creator, "page", s.LeadPagePublicId, "7d");
        var salesPage = await SeriesAsync(s.Creator, "page", s.SalesPagePublicId, "7d");
        Assert.Equal((2L, 1L), (leadPage.Totals.Downloads, salesPage.Totals.Downloads));   // the direct order belongs to no page

        var workspace = await SeriesAsync(s.Creator, "workspace", Guid.Empty, "7d");
        Assert.Equal((4L, 1L), (workspace.Totals.Downloads, workspace.PreviousTotals!.Downloads));
        Assert.Equal(workspace.Totals.Downloads, workspace.Buckets.Sum(b => b.Downloads));
    }

    [Fact]
    public async Task AllTime_CountsEveryDownload_AndNamesTheBestMonth()
    {
        var s = await SeedAsync();
        await DownloadAsync(s.SignupToken, LocalNoon(0));
        await DownloadAsync(s.SignupToken, LocalNoon(0).AddMinutes(1));

        var series = await SeriesAsync(s.Creator, "product", s.FreeProduct, "all");

        Assert.Equal(2L, series.Totals.Downloads);
        Assert.Null(series.PreviousTotals);
        Assert.Equal(TimeZoneInfo.ConvertTime(LocalNoon(0), Zone).ToString("yyyy-MM"), series.BestMonth!.Downloads!.Month);
        Assert.Equal(2L, series.BestMonth.Downloads.Value);
    }
}
