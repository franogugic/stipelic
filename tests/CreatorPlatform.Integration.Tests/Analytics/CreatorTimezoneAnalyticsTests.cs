using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>The dashboard trends, the home summary's "this month" and the contact growth use the creator's months:
/// 23:30 UTC on a month's last day is already the next month in Europe/Zagreb.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CreatorTimezoneAnalyticsTests : IAsyncLifetime
{
    private const string Zagreb = "Europe/Zagreb";
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly TestRedisClient _redis;

    public CreatorTimezoneAnalyticsTests(PostgresFixture fixture, RedisFixture redis)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
        _redis = RedisFixture.CreateClient(redis.CreateOptions());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    private OrderService Orders(Shared.Infrastructure.Persistence.CreatorPlatformDbContext db) =>
        new(new OrderRepository(db), new HomeSummaryCache(_redis.JsonCache), new DashboardTrendsCache(_redis.JsonCache));

    /// <summary>23:30 UTC on the last day of the previous UTC month, and the local month it falls in.</summary>
    private static (DateTimeOffset At, string LocalMonth, string UtcMonth) LateNight()
    {
        var now = DateTimeOffset.UtcNow;
        var at = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1).AddMinutes(-30);
        return (at, TimeZoneInfo.ConvertTime(at, Zone).ToString("yyyy-MM"), at.ToString("yyyy-MM"));
    }

    [Fact]
    public async Task DashboardTrends_BucketByTheCreatorsMonths()
    {
        var creator = await SeedCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId);
        var (at, localMonth, utcMonth) = LateNight();
        await AddOrderAsync(creator.CreatorId, productId, 2500, at);

        await using var db = _fixture.CreateDbContext();
        var trends = await Orders(db).GetDashboardTrendsAsync(creator.Slug, creator.OwnerUserId, "12m", CancellationToken.None);

        Assert.Equal(12, trends.Points.Count);
        Assert.Equal(2500, trends.Points.Single(p => p.BucketStart == $"{localMonth}-01").RevenueCents);
        Assert.DoesNotContain(trends.Points, p => p.BucketStart == $"{utcMonth}-01" && p.RevenueCents > 0);
        Assert.Equal(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).ToString("yyyy-MM") + "-01", trends.Points[^1].BucketStart);
    }

    [Fact]
    public async Task HomeSummary_ThisMonth_StartsAtTheCreatorsMidnight()
    {
        var creator = await SeedCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId);
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime;
        var monthStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, 1), Zone), TimeSpan.Zero);
        await AddOrderAsync(creator.CreatorId, productId, 1000, monthStart);                 // the first instant of this local month
        await AddOrderAsync(creator.CreatorId, productId, 700, monthStart.AddMinutes(-1));   // last month in Zagreb

        await using var db = _fixture.CreateDbContext();
        var summary = await Orders(db).GetHomeSummaryAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None);

        Assert.Equal(1000, summary.ThisMonthRevenueCents);
        Assert.Equal((1700, 1000), (summary.MonthlyRevenueTrend.Sum(), summary.MonthlyRevenueTrend[^1]));
    }

    [Fact]
    public async Task ContactGrowth_CountsByTheCreatorsMonths()
    {
        var creator = await SeedCreatorAsync();
        var (at, localMonth, utcMonth) = LateNight();
        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO marketing.contact_summaries ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds")
                VALUES ({creator.CreatorId}, 'late@example.test', {at}, {at}, ARRAY[]::int[])
                """);
        }

        await using var read = _fixture.CreateDbContext();
        var stats = await new ContactsService(
                new MarketingCreatorContextProvider(read), new ContactsRepository(read), new MarketingUnitOfWork(read), new CreatorUsageService(read), new UnsubscribeRepository(read))
            .GetStatsAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None);

        Assert.Equal(0, stats.Growth.Single(g => g.Month == utcMonth).Total);
        Assert.Equal(1, stats.Growth.Single(g => g.Month == localMonth).Total);
    }

    private async Task<TestData.SeededCreator> SeedCreatorAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creator.CreatorId}, 'Brand', '#E2553A', {Zagreb}, 'en', {now}, {now})
            """);
        return creator;
    }

    private async Task<int> AddProductAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName", "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Product', 1000, 'Active', 'productfile/test/guide.pdf', 'guide.pdf', 1024, 'application/pdf', now(), {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task AddOrderAsync(int creatorId, int productId, int amountCents, DateTimeOffset paidAt)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {TestData.UniqueEmail("buyer")}, {amountCents}, 'Eur', 'Paid',
                    1000, 100, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {paidAt}, {paidAt}, {paidAt})
            """);
    }
}
