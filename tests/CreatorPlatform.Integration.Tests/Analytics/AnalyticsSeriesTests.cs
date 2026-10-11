using CreatorPlatform.Analytics.Application.Series;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>The period series of a page and a product, bucketed in the creator's timezone (Europe/Zagreb here).
/// Times are relative to now; an assertion that depends on "today" can only drift in the minute around local
/// midnight.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AnalyticsSeriesTests
{
    private const string Zagreb = "Europe/Zagreb";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public AnalyticsSeriesTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<AnalyticsSeriesDto> SeriesAsync(TestData.SeededCreator creator, string scope, Guid id, string period)
    {
        await using var db = _fixture.CreateDbContext();
        return await new AnalyticsSeriesService(new AnalyticsSeriesReader(db))
            .GetAsync(creator.Slug, creator.OwnerUserId, scope, id, period, CancellationToken.None);
    }

    /// <summary>Local midnight today in Zagreb, as an instant.</summary>
    private static DateTimeOffset LocalMidnight()
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime.Date;
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }

    private static string LocalDay(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, Zone).ToString("yyyy-MM-dd'T'00:00");

    [Fact]
    public async Task Days_AreLocalDays_AndThePreviousPeriodSitsRightBefore()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var midnight = LocalMidnight();

        await AddOrderAsync(creator.CreatorId, productId, page.Id, 2900, midnight);                // 22:00/23:00 UTC yesterday, today in Zagreb
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 1000, midnight.AddMinutes(-1)); // yesterday in Zagreb
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 500, midnight.AddDays(-8));     // the 7 days before
        await AddViewAsync(page.Id, midnight);
        await AddViewAsync(page.Id, midnight.AddDays(-10));
        await AddCaptureAsync(page.Id, null, "ana@example.test", midnight);

        var series = await SeriesAsync(creator, "page", page.PublicId, "7d");

        Assert.Equal(("day", Zagreb, 7, 7), (series.Step, series.Timezone, series.Buckets.Count, series.PreviousBuckets!.Count));
        Assert.Equal(new AnalyticsTotalsDto(1, 1, 2, 3900), series.Totals);
        Assert.Equal(new AnalyticsTotalsDto(1, 0, 1, 500), series.PreviousTotals);
        var today = series.Buckets[^1];
        Assert.Equal(LocalDay(midnight), today.Start);
        Assert.Equal((1L, 1L, 1L, 2900L), (today.Views, today.Emails, today.Sales, today.RevenueCents));
        Assert.Equal((1L, 1000L), (series.Buckets[^2].Sales, series.Buckets[^2].RevenueCents));
        Assert.Null(series.BestMonth);
        Assert.Null(series.Product);
    }

    [Fact]
    public async Task Today_ComparesWithYesterdayUpToTheSameTime()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var now = DateTimeOffset.UtcNow;

        await AddOrderAsync(creator.CreatorId, productId, page.Id, 1000, now.AddDays(-1).AddMinutes(-1)); // before the cut
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 2000, now.AddDays(-1).AddMinutes(1));  // later yesterday
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 4000, now.AddMinutes(-1));

        var series = await SeriesAsync(creator, "page", page.PublicId, "today");

        Assert.Equal("hour", series.Step);
        Assert.Equal(24, series.PreviousBuckets!.Count);
        Assert.Equal(TimeZoneInfo.ConvertTime(now, Zone).Hour + 1, series.Buckets.Count);
        Assert.Equal((1L, 4000L), (series.Totals.Sales, series.Totals.RevenueCents));
        Assert.Equal((1L, 1000L), (series.PreviousTotals!.Sales, series.PreviousTotals.RevenueCents));
        Assert.Equal(3000L, series.PreviousBuckets.Sum(b => b.RevenueCents)); // the chart shows the whole of yesterday
    }

    [Fact]
    public async Task All_TotalsAreAllTime_TheChartTwelveLocalMonths_WithTheBestMonth()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync(createdAt: DateTimeOffset.UtcNow.AddYears(-2));
        var now = DateTimeOffset.UtcNow;
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 1000, now.AddYears(-1).AddMonths(-3)); // before the chart
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 7000, now.AddMonths(-2));
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 2000, now);

        var series = await SeriesAsync(creator, "page", page.PublicId, "all");

        Assert.Equal(("month", 12), (series.Step, series.Buckets.Count));
        Assert.Null(series.PreviousTotals);
        Assert.Equal((3L, 10000L), (series.Totals.Sales, series.Totals.RevenueCents));
        Assert.Equal(9000L, series.Buckets.Sum(b => b.RevenueCents));
        Assert.Equal(TimeZoneInfo.ConvertTime(now.AddMonths(-2), Zone).ToString("yyyy-MM"), series.BestMonth!.Revenue!.Month);
        Assert.Null(series.BestMonth.Views);
    }

    [Fact]
    public async Task AProduct_SumsItsPages_DeduplicatesEmails_AndAddsRefundsAndItsShare()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var second = await AddPageAsync(creator.CreatorId, productId, DateTimeOffset.UtcNow.AddMonths(-1));
        var otherProductId = await AddProductAsync(creator.CreatorId);
        var otherPage = await AddPageAsync(creator.CreatorId, otherProductId, DateTimeOffset.UtcNow.AddMonths(-1));
        var at = LocalMidnight();

        await AddViewAsync(page.Id, at);
        await AddViewAsync(second.Id, at);
        await AddViewAsync(otherPage.Id, at);
        await AddCaptureAsync(page.Id, productId, "ana@example.test", at);
        await AddCaptureAsync(second.Id, productId, "ana@example.test", at.AddMinutes(5)); // the same person on both pages
        await AddCaptureAsync(second.Id, productId, "iva@example.test", at);
        await AddOrderAsync(creator.CreatorId, productId, second.Id, 3000, at);
        await AddOrderAsync(creator.CreatorId, otherProductId, otherPage.Id, 1000, at);
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 1500, at.AddDays(-40), status: "Refunded", updatedAt: at);

        var series = await SeriesAsync(creator, "product", (await PublicIdAsync("products.products", productId)), "30d");

        Assert.Equal(new AnalyticsTotalsDto(2, 2, 1, 3000), series.Totals);
        Assert.Equal(new AnalyticsProductExtrasDto(1, 1500, 4000), series.Product);
    }

    [Fact]
    public async Task AnotherCreatorsPage_Is404_AndUnknownScopeOrPeriodIs400()
    {
        var (_, _, page) = await SeedCreatorWithPageAsync();
        var other = await _data.CreateCreatorAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => SeriesAsync(other, "page", page.PublicId, "7d"));
        await Assert.ThrowsAsync<BadRequestException>(() => SeriesAsync(other, "campaign", page.PublicId, "7d"));
        await Assert.ThrowsAsync<BadRequestException>(() => SeriesAsync(other, "page", page.PublicId, "90d"));
    }

    // ---- seeding ---------------------------------------------------------------------------------------------

    private sealed record Page(int Id, Guid PublicId);

    private async Task<(TestData.SeededCreator Creator, int ProductId, Page Page)> SeedCreatorWithPageAsync(DateTimeOffset? createdAt = null)
    {
        var creator = await _data.CreateCreatorAsync();
        await using (var db = _fixture.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
                VALUES ({creator.CreatorId}, 'Brand', '#E2553A', {Zagreb}, 'en', {now}, {now})
                """);
        }
        var productId = await AddProductAsync(creator.CreatorId);
        var page = await AddPageAsync(creator.CreatorId, productId, createdAt ?? DateTimeOffset.UtcNow.AddMonths(-3));
        return (creator, productId, page);
    }

    private async Task<int> AddProductAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow.AddYears(-3);
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Product', 1000, 'Digital', 'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task<Page> AddPageAsync(int creatorId, int productId, DateTimeOffset createdAt)
    {
        await using var db = _fixture.CreateDbContext();
        var publicId = Guid.NewGuid();
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, {productId}, 'Page', {$"p-{Guid.NewGuid():N}"[..20]}, 'Sales', 'Published', {createdAt}, {createdAt})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return new Page(id, publicId);
    }

    private async Task AddViewAsync(int landingPageId, DateTimeOffset at)
    {
        await using var db = _fixture.CreateDbContext();
        var date = DateOnly.FromDateTime(at.UtcDateTime);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.page_views ("Id", "LandingPageId", "VisitorId", "ViewedAt", "ViewedDate")
            VALUES ({Guid.NewGuid()}, {landingPageId}, {Guid.NewGuid()}, {at}, {date})
            """);
    }

    private async Task AddCaptureAsync(int landingPageId, int? productId, string email, DateTimeOffset at)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.email_captures ("Id", "LandingPageId", "ProductId", "Email", "CapturedAt")
            VALUES ({Guid.NewGuid()}, {landingPageId}, {productId}, {email}, {at})
            """);
    }

    private async Task AddOrderAsync(
        int creatorId, int productId, int landingPageId, int amountCents, DateTimeOffset paidAt,
        string status = "Paid", DateTimeOffset? updatedAt = null)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "LandingPageId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {landingPageId}, {TestData.UniqueEmail("buyer")}, {amountCents}, 'Eur', {status},
                    1000, 100, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {paidAt}, {paidAt}, {updatedAt ?? paidAt})
            """);
    }

    private async Task<Guid> PublicIdAsync(string table, int id)
    {
        await using var db = _fixture.CreateDbContext();
        var sql = System.Runtime.CompilerServices.FormattableStringFactory.Create(
            $"SELECT \"PublicId\" AS \"Value\" FROM {table} WHERE \"Id\" = {{0}}", id);
        return (await db.Database.SqlQuery<Guid>(sql).ToListAsync()).Single();
    }
}
