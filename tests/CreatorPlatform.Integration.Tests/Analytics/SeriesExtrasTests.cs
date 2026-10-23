using CreatorPlatform.Analytics.Application.Series;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>R6.3: the series' extras for the workspace and a product — refunds by refund time, distinct buyers (current,
/// previous, returning) and the period's sign-ups that are still subscribed. A page has none.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SeriesExtrasTests
{
    private const string Zagreb = "Europe/Zagreb";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public SeriesExtrasTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private static DateTimeOffset LocalNoon(int days)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).Date.AddDays(days).AddHours(12);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }

    private async Task<AnalyticsSeriesDto> SeriesAsync(TestData.SeededCreator creator, string scope, Guid id, string period)
    {
        await using var db = _fixture.CreateDbContext();
        return await new AnalyticsSeriesService(new AnalyticsSeriesReader(db)).GetAsync(
            creator.Slug, creator.OwnerUserId, scope, id, period, CancellationToken.None);
    }

    private async Task<TestData.SeededCreator> CreatorAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creator.CreatorId}, 'Brand', '#E2553A', {Zagreb}, 'en', now(), now())
            """);
        return creator;
    }

    private async Task<(int Id, Guid PublicId)> ProductAsync(int creatorId, int priceCents = 2900)
    {
        await using var db = _fixture.CreateDbContext();
        var publicId = Guid.NewGuid();
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName",
                                           "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, 'Product', {priceCents}, 'Active', 'ProductFile/test/p.pdf', 'p.pdf', 1024,
                    'application/pdf', now(), now() - interval '400 days', now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return (id, publicId);
    }

    /// <summary>A Paid order counted at <paramref name="paidAt"/>, or a Refunded one refunded at <paramref name="refundedAt"/>.</summary>
    private async Task OrderAsync(int creatorId, int productId, string email, DateTimeOffset paidAt, DateTimeOffset? refundedAt = null,
        int amountCents = 1000)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "PaidAt", "UpdatedAt", "RefundedCents")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {email}, {amountCents}, 'Eur', {(refundedAt is null ? "Paid" : "Refunded")},
                    0, 0, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {paidAt}, {paidAt}, {refundedAt ?? paidAt},
                    {(refundedAt is null ? 0 : amountCents)})
            """);
    }

    private async Task<int> LeadPageAsync(int creatorId, int productId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, 'Lead', {$"p-{Guid.NewGuid():N}"}, 'LeadGen', 'Published', now() - interval '400 days', now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    /// <summary>A sign-up on <paramref name="pageId"/> at <paramref name="at"/>, and the contact it created (first capture).</summary>
    private async Task SignupAsync(int creatorId, int pageId, string email, DateTimeOffset at)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.email_captures ("Id", "LandingPageId", "Email", "CapturedAt") VALUES ({Guid.NewGuid()}, {pageId}, {email}, {at})
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.contact_summaries ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds")
            VALUES ({creatorId}, {email}, {at}, {at}, ARRAY[{pageId}]::integer[])
            ON CONFLICT ("CreatorId", "Email") DO UPDATE SET "LastCapturedAt" = EXCLUDED."LastCapturedAt"
            """);
    }

    [Fact]
    public async Task Customers_AreDistinctBuyers_ReturningOnesBoughtBefore_AndRefundsCountOnTheirRefundDay()
    {
        var creator = await CreatorAsync();
        var (productId, productPublicId) = await ProductAsync(creator.CreatorId);
        var (otherId, _) = await ProductAsync(creator.CreatorId);

        await OrderAsync(creator.CreatorId, productId, "ana@example.test", LocalNoon(-2));
        await OrderAsync(creator.CreatorId, productId, "ANA@example.test", LocalNoon(-1));    // the same buyer, other case
        await OrderAsync(creator.CreatorId, productId, "ana@example.test", LocalNoon(-45));   // before the period: Ana returns
        await OrderAsync(creator.CreatorId, otherId, "bo@example.test", LocalNoon(-3));
        await OrderAsync(creator.CreatorId, otherId, "bo@example.test", LocalNoon(-50));      // returning, but to another product
        await OrderAsync(creator.CreatorId, productId, "cy@example.test", LocalNoon(-40));    // previous period only
        await OrderAsync(creator.CreatorId, productId, "di@example.test", LocalNoon(-5), refundedAt: LocalNoon(-4), amountCents: 2900);
        await OrderAsync(creator.CreatorId, productId, "ed@example.test", LocalNoon(-60), refundedAt: LocalNoon(-35));   // refunded before

        var workspace = (await SeriesAsync(creator, "workspace", Guid.Empty, "30d")).Extras!;
        Assert.Equal((1, 2900L), (workspace.RefundCount, workspace.RefundedCents));
        Assert.Equal(2, workspace.Customers);              // Ana and Bo; Di's order was refunded
        Assert.Equal(3, workspace.PreviousCustomers);      // Ana, Bo, Cy
        Assert.Equal(2, workspace.ReturningCustomers);     // both bought before

        var product = (await SeriesAsync(creator, "product", productPublicId, "30d")).Extras!;
        Assert.Equal((1, 2900L), (product.RefundCount, product.RefundedCents));
        Assert.Equal((1, 2, 1), (product.Customers, product.PreviousCustomers!.Value, product.ReturningCustomers));   // Ana; Ana + Cy

        var all = (await SeriesAsync(creator, "workspace", Guid.Empty, "all")).Extras!;
        Assert.Equal((3, (int?)null, 0), (all.Customers, all.PreviousCustomers, all.ReturningCustomers));   // Ana, Bo, Cy
        Assert.Equal(2, all.RefundCount);
    }

    [Fact]
    public async Task Signups_AreThePeriodsDistinctEmails_AndTheOnesStillSubscribed()
    {
        var creator = await CreatorAsync();
        var (freeId, freePublicId) = await ProductAsync(creator.CreatorId, priceCents: 0);
        var page = await LeadPageAsync(creator.CreatorId, freeId);
        var secondPage = await LeadPageAsync(creator.CreatorId, freeId);
        var otherPage = await LeadPageAsync(creator.CreatorId, (await ProductAsync(creator.CreatorId, 0)).Id);

        await SignupAsync(creator.CreatorId, page, "a@example.test", LocalNoon(-1));
        await SignupAsync(creator.CreatorId, secondPage, "a@example.test", LocalNoon(0));   // on the product's other page: still one
        await SignupAsync(creator.CreatorId, page, "b@example.test", LocalNoon(-2));
        await SignupAsync(creator.CreatorId, page, "c@example.test", LocalNoon(-3));
        await SignupAsync(creator.CreatorId, page, "old@example.test", LocalNoon(-20)); // before the 7 days
        await SignupAsync(creator.CreatorId, otherPage, "d@example.test", LocalNoon(-1));
        await _data.UnsubscribeAsync(creator.CreatorId, "b@example.test");

        var product = (await SeriesAsync(creator, "product", freePublicId, "7d")).Extras!;
        Assert.Equal((3, 2), (product.Signups, product.SignupsStillSubscribed));
        Assert.Equal((0, 0, 0), (product.Customers, product.ReturningCustomers, product.RefundCount));

        var workspace = (await SeriesAsync(creator, "workspace", Guid.Empty, "7d")).Extras!;
        Assert.Equal((4, 3), (workspace.Signups, workspace.SignupsStillSubscribed));   // new contacts a, b, c, d
    }

    [Fact]
    public async Task APage_HasNoExtras()
    {
        var creator = await CreatorAsync();
        var (productId, _) = await ProductAsync(creator.CreatorId, 0);
        var page = await LeadPageAsync(creator.CreatorId, productId);
        await using var db = _fixture.CreateDbContext();
        var pagePublicId = (await db.Database.SqlQuery<Guid>($"""SELECT "PublicId" AS "Value" FROM landing_pages.landing_pages WHERE "Id" = {page}""").ToListAsync()).Single();

        var series = await SeriesAsync(creator, "page", pagePublicId, "7d");

        Assert.Null(series.Extras);
        Assert.Null(series.Product);
    }
}
