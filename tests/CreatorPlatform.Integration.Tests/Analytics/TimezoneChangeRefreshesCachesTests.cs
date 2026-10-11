using CreatorPlatform.Analytics.Infrastructure.Services;
using CreatorPlatform.Api.Caching;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>Saving a new timezone drops the creator's cached analytics, so the next read is bucketed in the new zone.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TimezoneChangeRefreshesCachesTests : IDisposable
{
    private const string Zagreb = "Europe/Zagreb";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    // One process-wide cache, as in production; services are per request.
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly HomeSummaryCache _homeSummaryCache;
    private readonly DashboardTrendsCache _trendsCache;

    public TimezoneChangeRefreshesCachesTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
        _homeSummaryCache = new HomeSummaryCache(_memoryCache);
        _trendsCache = new DashboardTrendsCache(_memoryCache);
    }

    public void Dispose() => _memoryCache.Dispose();

    private async Task<(int ThisMonth, long TrendThisMonth)> ReadAsync(TestData.SeededCreator creator)
    {
        await using var db = _fixture.CreateDbContext();
        var orders = new OrderService(new OrderRepository(db), _homeSummaryCache, _trendsCache);
        var summary = await orders.GetHomeSummaryAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None);
        var trends = await orders.GetDashboardTrendsAsync(creator.Slug, creator.OwnerUserId, "6m", CancellationToken.None);
        return (summary.ThisMonthRevenueCents, trends.Points[^1].RevenueCents);
    }

    private async Task SaveTimezoneAsync(TestData.SeededCreator creator, string timezone, ICreatorCacheInvalidator invalidator)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new CreatorService(
            new CreatorRepository(db),
            new CreatorMemberRepository(db),
            new CreatorPlanRepository(db),
            new CreatorSettingsRepository(db),
            new CreatorSubscriptionRepository(db),
            new CreatorPayoutProfileRepository(db),
            new CreatorsUnitOfWork(db),
            new FakeCheckoutSessionService(),
            new UnexpectedSubscriptionCancellationService(),
            new UnexpectedBillingPortalService(),
            new CreatorOpenBalanceCheck(db),
            new UnexpectedBillingCustomerService(),
            invalidator,
            NullLogger<CreatorService>.Instance);
        await service.UpdateSettingsAsync(creator.Slug, creator.OwnerUserId, new UpdateCreatorSettingsRequestDto
        {
            BrandName = "Brand",
            PrimaryColor = "#E2553A",
            Timezone = timezone,
            Language = "en",
        }, CancellationToken.None);
    }

    [Fact]
    public async Task ANewTimezone_RebuildsTheHomeSummaryAndTheTrendsInIt()
    {
        var creator = await _data.CreateCreatorAsync();
        await SeedSettingsAsync(creator.CreatorId, "UTC");
        // The first instant of this month in Zagreb: still last month in UTC.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime;
        var zagrebMonthStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(new DateTime(local.Year, local.Month, 1), zone), TimeSpan.Zero);
        await AddPaidOrderAsync(creator.CreatorId, 1500, zagrebMonthStart);

        Assert.Equal((0, 0L), await ReadAsync(creator)); // cached in UTC

        var invalidator = new CreatorCacheInvalidator(_homeSummaryCache, new ViewsSummaryCache(_memoryCache), _trendsCache);
        await SaveTimezoneAsync(creator, Zagreb, invalidator);

        Assert.Equal((1500, 1500L), await ReadAsync(creator));
    }

    [Fact]
    public async Task SavingTheSameTimezone_LeavesTheCachesAlone()
    {
        var creator = await _data.CreateCreatorAsync();
        await SeedSettingsAsync(creator.CreatorId, Zagreb);
        var recording = new RecordingCreatorCacheInvalidator();

        await SaveTimezoneAsync(creator, Zagreb, recording);
        Assert.Empty(recording.Invalidated);

        await SaveTimezoneAsync(creator, "Europe/Lisbon", recording);
        Assert.Equal([creator.CreatorId], recording.Invalidated);
    }

    private async Task SeedSettingsAsync(int creatorId, string timezone)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, 'Brand', '#E2553A', {timezone}, 'en', {now}, {now})
            """);
    }

    private async Task AddPaidOrderAsync(int creatorId, int amountCents, DateTimeOffset paidAt)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var productId = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Product', 1000, 'Digital', 'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {TestData.UniqueEmail("buyer")}, {amountCents}, 'Eur', 'Paid',
                    1000, 100, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {paidAt}, {paidAt}, {paidAt})
            """);
    }
}
