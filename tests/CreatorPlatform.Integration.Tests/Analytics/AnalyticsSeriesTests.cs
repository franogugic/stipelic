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

    /// <summary>Noon, <paramref name="days"/> local days from today (negative = past). Noon keeps a DST change inside the
    /// range from moving the event to another local day.</summary>
    private static DateTimeOffset LocalNoon(int days)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).Date.AddDays(days).AddHours(12);
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
        Assert.Equal(new AnalyticsTotalsDto(1, 1, 2, 3900, 0), series.Totals);
        Assert.Equal(new AnalyticsTotalsDto(1, 0, 1, 500, 0), series.PreviousTotals);
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

        Assert.Equal(new AnalyticsTotalsDto(2, 2, 1, 3000, 0), series.Totals);
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

    [Fact]
    public async Task FifteenDays_AreFifteenLocalDays_WithTheFifteenBeforeAsThePreviousPeriod()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var noon = LocalNoon(0);

        await AddOrderAsync(creator.CreatorId, productId, page.Id, 1000, noon);
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 2000, LocalNoon(-14)); // the first day of the window
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 4000, LocalNoon(-15)); // the previous period's last day
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 8000, LocalNoon(-29)); // the previous period's first day
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 16000, LocalNoon(-30)); // before both

        var series = await SeriesAsync(creator, "page", page.PublicId, "15d");

        Assert.Equal(("day", 15, 15), (series.Step, series.Buckets.Count, series.PreviousBuckets!.Count));
        Assert.Equal((2L, 3000L), (series.Totals.Sales, series.Totals.RevenueCents));
        Assert.Equal((2L, 12000L), (series.PreviousTotals!.Sales, series.PreviousTotals.RevenueCents));
        Assert.Equal(2000L, series.Buckets[0].RevenueCents);
        Assert.Equal(1000L, series.Buckets[^1].RevenueCents);
        Assert.Equal(4000L, series.PreviousBuckets[^1].RevenueCents);
        Assert.Equal(8000L, series.PreviousBuckets[0].RevenueCents);
    }

    [Theory]
    [InlineData("2m", 2)]
    [InlineData("3m", 3)]
    public async Task TwoAndThreeMonths_AreWeeksFromTheDayAfterTheSameDateNMonthsAgo_AndEveryEventLandsInItsOwnWeek(string period, int months)
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).Date;
        var first = today.AddMonths(-months).AddDays(1);
        var previousFirst = first.AddMonths(-months);
        int Offset(DateTime day) => (day - today).Days;

        // Today (the last, partial week), the window's first day, a day in the middle; the previous window's first and
        // last days; and a day before both windows.
        var inWindow = new[] { 0, Offset(first), Offset(first) / 2 };
        var inPrevious = new[] { Offset(previousFirst), Offset(first) - 1 };
        foreach (var offset in inWindow.Concat(inPrevious))
            await AddOrderAsync(creator.CreatorId, productId, page.Id, 1000, LocalNoon(offset));
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 99000, LocalNoon(Offset(previousFirst) - 1));

        var series = await SeriesAsync(creator, "page", page.PublicId, period);

        var weeks = ((today - first).Days + 1 + 6) / 7;
        var previousWeeks = ((first - previousFirst).Days + 6) / 7;
        Assert.Equal(("week", weeks, previousWeeks), (series.Step, series.Buckets.Count, series.PreviousBuckets!.Count));
        Assert.Equal((3L, 3000L), (series.Totals.Sales, series.Totals.RevenueCents));
        Assert.Equal((2L, 2000L), (series.PreviousTotals!.Sales, series.PreviousTotals.RevenueCents));
        Assert.Equal(first.ToString("yyyy-MM-dd'T'00:00"), series.Buckets[0].Start);
        Assert.Equal(first.AddDays(7).ToString("yyyy-MM-dd'T'00:00"), series.Buckets[1].Start);
        Assert.Equal(previousFirst.ToString("yyyy-MM-dd'T'00:00"), series.PreviousBuckets[0].Start);
        foreach (var offset in inWindow)
            Assert.Equal(1L, series.Buckets[(offset - Offset(first)) / 7].Sales);
        Assert.Equal(1L, series.PreviousBuckets[0].Sales);          // the previous window's first day
        Assert.Equal(1L, series.PreviousBuckets[^1].Sales);         // its last day, the day before the window
    }

    [Fact]
    public async Task SixMonths_AreSixMonthlyBucketsAnchoredAtTheFirstDay_ComparedWithTheSixMonthsBefore()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).Date;
        var first = today.AddMonths(-6).AddDays(1);
        int Offset(DateTime day) => (day - today).Days;

        await AddOrderAsync(creator.CreatorId, productId, page.Id, 1000, LocalNoon(Offset(first)));                 // month 1, first day
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 2000, LocalNoon(Offset(first.AddMonths(1)) - 1)); // month 1, last day
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 3000, LocalNoon(Offset(first.AddMonths(1))));     // month 2
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 4000, LocalNoon(0));                              // month 6 (today)
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 5000, LocalNoon(Offset(first) - 1));              // the previous window's last day
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 6000, LocalNoon(Offset(first.AddMonths(-6))));    // its first day

        var series = await SeriesAsync(creator, "page", page.PublicId, "6m");

        Assert.Equal(("month", 6, 6), (series.Step, series.Buckets.Count, series.PreviousBuckets!.Count));
        Assert.Equal(
            Enumerable.Range(0, 6).Select(i => first.AddMonths(i).ToString("yyyy-MM-dd'T'00:00")),
            series.Buckets.Select(b => b.Start));
        Assert.Equal([3000L, 3000L, 0L, 0L, 0L, 4000L], series.Buckets.Select(b => b.RevenueCents));
        Assert.Equal((4L, 10000L), (series.Totals.Sales, series.Totals.RevenueCents));
        Assert.Equal(6000L, series.PreviousBuckets[0].RevenueCents);
        Assert.Equal(5000L, series.PreviousBuckets[^1].RevenueCents);
        Assert.Equal(11000L, series.PreviousTotals!.RevenueCents);
    }

    [Fact]
    public async Task TheWorkspace_SumsAllItsPages_CountsNewContacts_AndAllPaidOrders()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var second = await AddPageAsync(creator.CreatorId, productId, DateTimeOffset.UtcNow.AddMonths(-1));
        var otherProductId = await AddProductAsync(creator.CreatorId);
        var otherCreator = await _data.CreateCreatorAsync();
        var (foreign, foreignProductId, foreignPage) = await SeedCreatorWithPageAsync();
        var at = LocalNoon(0);

        await AddViewAsync(page.Id, at);
        await AddViewAsync(second.Id, at);
        await AddViewAsync(second.Id, at.AddDays(-20));
        await AddViewAsync(foreignPage.Id, at);                                          // another workspace
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 3000, at);
        await AddOrderAsync(creator.CreatorId, otherProductId, second.Id, 1000, at.AddDays(-1));
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 500, at.AddDays(-20));
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 700, at, status: "Refunded"); // not a sale
        await AddOrderAsync(foreign.CreatorId, foreignProductId, foreignPage.Id, 9999, at);
        // Emails are new contacts here: a person captured on two pages is one contact, counted when first captured.
        await AddCaptureAsync(page.Id, productId, "ana@example.test", at);
        await AddCaptureAsync(second.Id, productId, "ana@example.test", at.AddMinutes(5));
        await AddContactAsync(creator.CreatorId, "ana@example.test", at);
        await AddContactAsync(creator.CreatorId, "iva@example.test", at.AddDays(-3));
        await AddContactAsync(creator.CreatorId, "old@example.test", at.AddDays(-40));
        await AddContactAsync(otherCreator.CreatorId, "x@example.test", at);

        var series = await SeriesAsync(creator, "workspace", Guid.Empty, "30d");

        Assert.Equal(("workspace", "day", 30), (series.Scope, series.Step, series.Buckets.Count));
        Assert.Equal(new AnalyticsTotalsDto(3, 2, 3, 4500, 0), series.Totals);
        Assert.Equal(new AnalyticsTotalsDto(0, 1, 0, 0, 0), series.PreviousTotals);        // old@ joined 40 days ago
        Assert.Equal((2L, 1L, 1L, 3000L), (series.Buckets[^1].Views, series.Buckets[^1].Emails, series.Buckets[^1].Sales, series.Buckets[^1].RevenueCents));
        Assert.Equal(1L, series.Buckets[^4].Emails);                                     // iva@, three days ago
        Assert.Null(series.Product);

        var all = await SeriesAsync(creator, "workspace", Guid.Empty, "all");
        Assert.Equal((3L, 3L, 3L, 4500L), (all.Totals.Views, all.Totals.Emails, all.Totals.Sales, all.Totals.RevenueCents));
    }

    [Fact]
    public async Task TheWorkspace_IsAlwaysTheCallersOwn()
    {
        var (creator, _, _) = await SeedCreatorWithPageAsync();
        var other = await _data.CreateCreatorAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => SeriesAsync(other with { Slug = creator.Slug }, "workspace", Guid.Empty, "7d"));
    }

    // ---- revenue by product (F6) -----------------------------------------------------------------------------

    private async Task<AnalyticsProductBreakdownDto> BreakdownAsync(TestData.SeededCreator creator, string? period, Guid? productId = null)
    {
        await using var db = _fixture.CreateDbContext();
        return await new AnalyticsSeriesService(new AnalyticsSeriesReader(db))
            .GetProductBreakdownAsync(creator.Slug, creator.OwnerUserId, period, productId, CancellationToken.None);
    }

    [Fact]
    public async Task ProductRevenue_IsRankedAndCappedAtTwenty_WithTheRestAsOther()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var products = new List<int> { productId };
        for (var i = 1; i < 22; i++)
            products.Add(await AddProductAsync(creator.CreatorId, $"Product {i:00}"));
        var freeProductId = await AddProductAsync(creator.CreatorId, "Free guide");
        var at = LocalNoon(0);

        for (var i = 0; i < products.Count; i++)
            await AddOrderAsync(creator.CreatorId, products[i], page.Id, 1000 + i * 100, at);      // 1000 … 3100, all different
        await AddOrderAsync(creator.CreatorId, products[^1], page.Id, 500, at.AddDays(-1));         // a second sale of the best one
        await AddOrderAsync(creator.CreatorId, freeProductId, page.Id, 0, at);                      // a free sale: no revenue, left out
        await AddOrderAsync(creator.CreatorId, products[0], page.Id, 7777, at, status: "Refunded"); // refunded: not revenue
        await AddOrderAsync(creator.CreatorId, products[0], page.Id, 5555, at.AddDays(-45));         // outside the 30 days

        var breakdown = await BreakdownAsync(creator, "30d");

        Assert.Equal("30d", breakdown.Period);
        Assert.Equal(20, breakdown.Products.Count);
        Assert.Equal(Enumerable.Range(0, 22).Sum(i => 1000L + i * 100) + 500, breakdown.TotalRevenueCents);
        Assert.Equal(breakdown.Products.OrderByDescending(p => p.RevenueCents).Select(p => p.RevenueCents), breakdown.Products.Select(p => p.RevenueCents));
        Assert.Equal((3600L, 2L), (breakdown.Products[0].RevenueCents, breakdown.Products[0].Sales)); // 3100 + 500
        Assert.Equal(1000L + 1100L, breakdown.OtherRevenueCents);                                      // the two weakest products
        Assert.DoesNotContain(breakdown.Products, p => p.RevenueCents == 0);
        Assert.Equal(breakdown.TotalRevenueCents, breakdown.Products.Sum(p => p.RevenueCents) + breakdown.OtherRevenueCents);

        var all = await BreakdownAsync(creator, "all");
        Assert.Equal(breakdown.TotalRevenueCents + 5555, all.TotalRevenueCents);
    }

    [Fact]
    public async Task ProductRevenue_OfAFewProducts_HasNoOther_AndOnlyTheCallersOwn()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var (foreign, foreignProductId, foreignPage) = await SeedCreatorWithPageAsync();
        await AddOrderAsync(creator.CreatorId, productId, page.Id, 2500, LocalNoon(0));
        await AddOrderAsync(foreign.CreatorId, foreignProductId, foreignPage.Id, 9999, LocalNoon(0));

        var breakdown = await BreakdownAsync(creator, null); // default: 30d

        Assert.Equal("30d", breakdown.Period);
        Assert.Equal((2500L, 0L), (breakdown.TotalRevenueCents, breakdown.OtherRevenueCents));
        Assert.Equal([2500L], breakdown.Products.Select(p => p.RevenueCents));
        Assert.Equal("Eur", breakdown.Currency);

        await Assert.ThrowsAsync<NotFoundException>(() => BreakdownAsync(foreign with { Slug = creator.Slug }, "30d"));
        await Assert.ThrowsAsync<BadRequestException>(() => BreakdownAsync(creator, "90d"));
    }

    [Fact]
    public async Task ProductRevenue_OfAWorkspaceWithoutSales_IsEmpty()
    {
        var (creator, _, _) = await SeedCreatorWithPageAsync();

        var breakdown = await BreakdownAsync(creator, "7d");

        Assert.Equal((0L, 0L), (breakdown.TotalRevenueCents, breakdown.OtherRevenueCents));
        Assert.Empty(breakdown.Products);
    }

    [Fact]
    public async Task ASelectedProduct_IsRankedEvenBeyondTheTopTwenty_AndOneWithoutRevenueHasNoRank()
    {
        var (creator, productId, page) = await SeedCreatorWithPageAsync();
        var products = new List<int> { productId };
        for (var i = 1; i < 22; i++)
            products.Add(await AddProductAsync(creator.CreatorId, $"Product {i:00}"));
        var unsold = await AddProductAsync(creator.CreatorId, "Unsold");
        for (var i = 0; i < products.Count; i++)
            await AddOrderAsync(creator.CreatorId, products[i], page.Id, 1000 + i * 100, LocalNoon(0));   // products[0] is the weakest

        var weakest = await BreakdownAsync(creator, "30d", await PublicIdAsync("products.products", productId));
        var none = await BreakdownAsync(creator, "30d", await PublicIdAsync("products.products", unsold));
        var plain = await BreakdownAsync(creator, "30d");

        Assert.Equal(22, weakest.ProductsWithSales);
        Assert.Equal(20, weakest.Products.Count);
        Assert.Equal((22, 1000L, 1L), (weakest.Selected!.Rank!.Value, weakest.Selected.RevenueCents, weakest.Selected.Sales));
        Assert.Equal(("Unsold", (int?)null, 0L), (none.Selected!.Name, none.Selected.Rank, none.Selected.RevenueCents));
        Assert.Null(plain.Selected);
        await Assert.ThrowsAsync<NotFoundException>(() => BreakdownAsync(creator, "30d", Guid.NewGuid()));
    }

    // ---- top pages and product sign-ups (R6.4) ---------------------------------------------------------------

    private async Task<AnalyticsPagesDto> TopPagesAsync(TestData.SeededCreator creator, string? period, Guid? productId = null, int? limit = null)
    {
        await using var db = _fixture.CreateDbContext();
        return await new AnalyticsSeriesService(new AnalyticsSeriesReader(db))
            .GetTopPagesAsync(creator.Slug, creator.OwnerUserId, period, productId, limit, CancellationToken.None);
    }

    private async Task<ProductSignupsDto> SignupsAsync(TestData.SeededCreator creator, Guid productId, int? limit = null)
    {
        await using var db = _fixture.CreateDbContext();
        return await new AnalyticsSeriesService(new AnalyticsSeriesReader(db))
            .GetProductSignupsAsync(creator.Slug, creator.OwnerUserId, productId, limit, CancellationToken.None);
    }

    private async Task<Page> AddPageOfAsync(int creatorId, int productId, string title, string type, string status)
    {
        await using var db = _fixture.CreateDbContext();
        var publicId = Guid.NewGuid();
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, {productId}, {title}, {$"p-{Guid.NewGuid():N}"[..20]}, {type}, {status}, now() - interval '90 days', now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return new Page(id, publicId);
    }

    [Fact]
    public async Task TopPages_RankByRevenueThenEmailsThenViews_InThePeriod_LeavingOutArchivedAndIdlePages()
    {
        var (creator, productId, seller) = await SeedCreatorWithPageAsync();      // "Page", Sales
        var otherProductId = await AddProductAsync(creator.CreatorId, "Other");
        var lead = await AddPageOfAsync(creator.CreatorId, otherProductId, "Lead", "LeadGen", "Published");
        var viewed = await AddPageOfAsync(creator.CreatorId, otherProductId, "Viewed", "Sales", "Draft");
        var archived = await AddPageOfAsync(creator.CreatorId, productId, "Archived", "Sales", "Archived");
        await AddPageOfAsync(creator.CreatorId, productId, "Idle", "Sales", "Published");
        var at = LocalNoon(-1);

        await AddOrderAsync(creator.CreatorId, productId, seller.Id, 2900, at);
        await AddOrderAsync(creator.CreatorId, productId, seller.Id, 9900, LocalNoon(-40));           // before the 30 days
        await AddOrderAsync(creator.CreatorId, productId, seller.Id, 5000, at, status: "Refunded");    // not a sale
        await AddViewAsync(seller.Id, at);
        await AddCaptureAsync(lead.Id, otherProductId, "a@example.test", at);
        await AddCaptureAsync(lead.Id, otherProductId, "b@example.test", at);
        for (var i = 0; i < 5; i++)
            await AddViewAsync(viewed.Id, at);
        await AddOrderAsync(creator.CreatorId, productId, archived.Id, 99000, at);                     // archived: left out

        var top = await TopPagesAsync(creator, null);   // default 30d

        Assert.Equal(("30d", 4), (top.Period, top.PageCount));   // the archived page is not counted
        Assert.Equal(["Page", "Lead", "Viewed"], top.Pages.Select(p => p.Title));
        var first = top.Pages[0];
        Assert.Equal((1L, 2900L, 1L, 0L), (first.Sales, first.RevenueCents, first.Views, first.Emails));
        Assert.Equal(("Sales", "Published", "Product"), (first.Type, first.Status, first.ProductName));
        Assert.Equal((2L, "LeadGen"), (top.Pages[1].Emails, top.Pages[1].Type));
        Assert.Equal((5L, "Draft"), (top.Pages[2].Views, top.Pages[2].Status));

        var limited = await TopPagesAsync(creator, "30d", limit: 1);
        Assert.Equal(["Page"], limited.Pages.Select(p => p.Title));
        var ofProduct = await TopPagesAsync(creator, "30d", await PublicIdAsync("products.products", otherProductId));
        Assert.Equal(2, ofProduct.PageCount);
        Assert.Equal(["Lead", "Viewed"], ofProduct.Pages.Select(p => p.Title));
        var allTime = await TopPagesAsync(creator, "all");
        Assert.Equal(2900L + 9900L, allTime.Pages[0].RevenueCents);

        await Assert.ThrowsAsync<BadRequestException>(() => TopPagesAsync(creator, "30d", limit: 21));
        await Assert.ThrowsAsync<NotFoundException>(() => TopPagesAsync(creator, "30d", Guid.NewGuid()));
    }

    [Fact]
    public async Task ProductSignups_AreTheNewestPerEmail_AcrossItsLeadPages_WithTheTotal()
    {
        var (creator, _, _) = await SeedCreatorWithPageAsync();
        var guide = await AddProductAsync(creator.CreatorId, "Guide");
        var first = await AddPageOfAsync(creator.CreatorId, guide, "First lead", "LeadGen", "Published");
        var second = await AddPageOfAsync(creator.CreatorId, guide, "Second lead", "LeadGen", "Archived");   // archived still counts
        var sales = await AddPageOfAsync(creator.CreatorId, guide, "Sales", "Sales", "Published");
        await AddCaptureAsync(first.Id, guide, "ana@example.test", LocalNoon(-5));
        await AddCaptureAsync(second.Id, guide, "ana@example.test", LocalNoon(-1));    // Ana's newest
        await AddCaptureAsync(first.Id, guide, "bo@example.test", LocalNoon(-3));
        await AddCaptureAsync(second.Id, guide, "cy@example.test", LocalNoon(-10));
        await AddCaptureAsync(sales.Id, guide, "di@example.test", LocalNoon(0));        // not a lead page
        await _data.UnsubscribeAsync(creator.CreatorId, "bo@example.test");
        var guidePublicId = await PublicIdAsync("products.products", guide);

        var signups = await SignupsAsync(creator, guidePublicId);
        var two = await SignupsAsync(creator, guidePublicId, limit: 2);

        Assert.Equal(3, signups.Total);
        Assert.Equal(["ana@example.test", "bo@example.test", "cy@example.test"], signups.Signups.Select(s => s.Email));
        Assert.Equal(("Second lead", second.PublicId, false), (signups.Signups[0].LandingPageTitle, signups.Signups[0].LandingPagePublicId, signups.Signups[0].Unsubscribed));
        Assert.True(signups.Signups[1].Unsubscribed);
        Assert.Equal((3, 2), (two.Total, two.Signups.Count));

        var none = await SignupsAsync(creator, await PublicIdAsync("products.products", await AddProductAsync(creator.CreatorId, "No pages")));
        Assert.Equal((0, 0), (none.Total, none.Signups.Count));
        await Assert.ThrowsAsync<NotFoundException>(() => SignupsAsync(creator, Guid.NewGuid()));
        await Assert.ThrowsAsync<BadRequestException>(() => SignupsAsync(creator, guidePublicId, limit: 0));
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

    private async Task<int> AddProductAsync(int creatorId, string name = "Product")
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow.AddYears(-3);
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName", "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {name}, 1000, 'Active', 'productfile/test/guide.pdf', 'guide.pdf', 1024, 'application/pdf', now(), {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task AddContactAsync(int creatorId, string email, DateTimeOffset firstCapturedAt)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.contact_summaries ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds")
            VALUES ({creatorId}, {email}, {firstCapturedAt}, {firstCapturedAt}, ARRAY[]::int[])
            """);
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
                                       "CreatedAt", "PaidAt", "UpdatedAt", "RefundedCents")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {landingPageId}, {TestData.UniqueEmail("buyer")}, {amountCents}, 'Eur', {status},
                    1000, 100, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {paidAt}, {paidAt}, {updatedAt ?? paidAt},
                    {(status == "Refunded" ? amountCents : 0)})
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
