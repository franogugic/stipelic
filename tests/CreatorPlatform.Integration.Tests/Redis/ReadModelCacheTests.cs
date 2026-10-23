using System.Text.Json;
using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Infrastructure.Services;
using CreatorPlatform.Api.Caching;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Infrastructure.Services;
using CreatorPlatform.Payments.Application.Dtos;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CreatorPlatform.Integration.Tests.Redis;

/// <summary>The four read-model caches on a real Redis: every DTO survives the JSON round trip, keys and TTLs are as
/// documented, and two API instances share entries and invalidations.</summary>
[Collection(RedisCollection.Name)]
public sealed class ReadModelCacheTests : IAsyncLifetime
{
    private const int CreatorId = 42;
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly RedisFixture _redis;
    private readonly string _prefix = RedisFixture.UniqueKeyPrefix();
    private readonly TestRedisClient _instanceA;
    private readonly TestRedisClient _instanceB;

    public ReadModelCacheTests(RedisFixture redis)
    {
        _redis = redis;
        // Two API instances: their own connection and circuit breaker each, one shared Redis and key prefix.
        _instanceA = RedisFixture.CreateClient(redis.CreateOptions(_prefix));
        _instanceB = RedisFixture.CreateClient(redis.CreateOptions(_prefix));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _instanceA.DisposeAsync();
        await _instanceB.DisposeAsync();
    }

    private IDatabase Raw => _redis.Multiplexer.GetDatabase();

    private static void AssertSameJson<T>(T expected, T? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    private static HomeSummaryDto HomeSummary(int landingPageCount = 3) => new(
        TotalPaidAmountCents: 123_456,
        PaidOrderCount: 17,
        Currency: "eur",
        ProductCount: 4,
        LandingPageCount: landingPageCount,
        RecentOrders:
        [
            new OrderDto(Guid.NewGuid(), "buyer@example.com", "Ana Anić", "Course", 4_900, "eur", "Paid",
                new DateTimeOffset(2026, 10, 21, 23, 30, 15, 123, TimeSpan.FromHours(2)),
                new DateTimeOffset(2026, 10, 21, 23, 31, 0, TimeSpan.FromHours(2)), 490, 4_410, "Launch page"),
            new OrderDto(Guid.NewGuid(), null, null, "Ebook", 1_000, "eur", "Pending",
                new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), null, 0, 1_000, null)
        ],
        ThisMonthRevenueCents: 9_800,
        TopProduct: new TopProductDto("Course", 88_200),
        RevenueTrend: [0, 4_900, 0, 1_000],
        EmailsSentThisMonth: 250,
        EmailsMonthlyLimit: 1_000,
        TotalPageViews: 3_210,
        SubscriberCount: 87,
        ViewsTrend: [10, 0, 33],
        MonthlyRevenueTrend: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
        TotalPlatformFeeCents: 12_345,
        MonthlyPaidOrderCounts: [0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
        CustomerCount: 15,
        RefundedOrderCount: 1,
        RefundedAmountCents: 1_000);

    private static List<LandingPageViewsSummaryDto> ViewsSummary(long views = 1_234) =>
        [new(Guid.NewGuid(), views, 987), new(Guid.NewGuid(), 0, 0)];

    [Fact]
    public async Task HomeSummary_RoundTripsEveryProperty_UnderItsKey_ForFiveMinutes()
    {
        var cache = new HomeSummaryCache(_instanceA.JsonCache);
        var summary = HomeSummary();

        await cache.SetAsync(CreatorId, summary, None);
        var read = await cache.GetAsync(CreatorId, None);

        AssertSameJson(summary, read);
        Assert.Equal(summary.RecentOrders[0].CreatedAt, read!.RecentOrders[0].CreatedAt);
        Assert.Equal(TimeSpan.FromHours(2), read.RecentOrders[0].CreatedAt.Offset);
        Assert.Null(read.RecentOrders[1].PaidAt);
        Assert.Equal(summary.TopProduct, read.TopProduct);
        await AssertTtlAsync($"{_prefix}home-summary:{CreatorId}", TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task ViewsSummary_RoundTrips_UnderItsKey_ForThreeMinutes()
    {
        var cache = new ViewsSummaryCache(_instanceA.JsonCache);
        var summary = ViewsSummary();

        await cache.SetAsync(CreatorId, summary, None);

        Assert.Equal(summary, await cache.GetAsync(CreatorId, None));
        await AssertTtlAsync($"{_prefix}views-summary:{CreatorId}", TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task PayoutSchedule_RoundTrips_WithAndWithoutAnchors_ForOneMinute()
    {
        var cache = new PayoutScheduleCache(_instanceA.JsonCache);
        var weekly = new PayoutScheduleDto("weekly", 7, "monday", null);
        var monthly = new PayoutScheduleDto("monthly", 3, null, 31);

        await cache.SetAsync(CreatorId, weekly, None);
        await cache.SetAsync(CreatorId + 1, monthly, None);

        Assert.Equal(weekly, await cache.GetAsync(CreatorId, None));
        Assert.Equal(monthly, await cache.GetAsync(CreatorId + 1, None));
        await AssertTtlAsync($"{_prefix}payout-schedule:{CreatorId}", TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task TwoInstances_ShareEntries()
    {
        await new HomeSummaryCache(_instanceA.JsonCache).SetAsync(CreatorId, HomeSummary(), None);
        await new ViewsSummaryCache(_instanceA.JsonCache).SetAsync(CreatorId, ViewsSummary(), None);
        await new PayoutScheduleCache(_instanceA.JsonCache).SetAsync(CreatorId, new("daily", 2, null, null), None);

        Assert.NotNull(await new HomeSummaryCache(_instanceB.JsonCache).GetAsync(CreatorId, None));
        Assert.NotNull(await new ViewsSummaryCache(_instanceB.JsonCache).GetAsync(CreatorId, None));
        Assert.NotNull(await new PayoutScheduleCache(_instanceB.JsonCache).GetAsync(CreatorId, None));
    }

    [Fact]
    public async Task AnInvalidationOnOneInstance_IsAMissOnTheOther()
    {
        var homeA = new HomeSummaryCache(_instanceA.JsonCache);
        var viewsA = new ViewsSummaryCache(_instanceA.JsonCache);
        await homeA.SetAsync(CreatorId, HomeSummary(), None);
        await viewsA.SetAsync(CreatorId, ViewsSummary(), None);

        await new CreatorCacheInvalidator(_instanceB.JsonCache).InvalidateAsync(CreatorId, None);

        Assert.Null(await homeA.GetAsync(CreatorId, None));
        Assert.Null(await viewsA.GetAsync(CreatorId, None));
        Assert.False(await Raw.KeyExistsAsync($"{_prefix}home-summary:{CreatorId}"));
        Assert.False(await Raw.KeyExistsAsync($"{_prefix}views-summary:{CreatorId}"));
    }

    [Fact]
    public async Task SingleRemoves_OnOneInstance_AreMissesOnTheOther()
    {
        var homeA = new HomeSummaryCache(_instanceA.JsonCache);
        var viewsA = new ViewsSummaryCache(_instanceA.JsonCache);
        await homeA.SetAsync(CreatorId, HomeSummary(), None);
        await viewsA.SetAsync(CreatorId, ViewsSummary(), None);

        await new HomeSummaryCache(_instanceB.JsonCache).RemoveAsync(CreatorId, None);
        await new ViewsSummaryCache(_instanceB.JsonCache).RemoveAsync(CreatorId, None);

        Assert.Null(await homeA.GetAsync(CreatorId, None));
        Assert.Null(await viewsA.GetAsync(CreatorId, None));
    }

    [Fact]
    public async Task AValueThatNoLongerDeserializes_IsAMiss_IsDropped_AndLogged()
    {
        var key = $"{_prefix}home-summary:{CreatorId}";
        await Raw.StringSetAsync(key, "{\"recentOrders\": \"not a list\"}", TimeSpan.FromMinutes(5));

        var read = await new HomeSummaryCache(_instanceA.JsonCache).GetAsync(CreatorId, None);

        Assert.Null(read);
        Assert.False(await Raw.KeyExistsAsync(key));
        Assert.Single(_instanceA.JsonCacheLog.Entries, entry => entry.Level == LogLevel.Warning);
    }

    private async Task AssertTtlAsync(string key, TimeSpan expected)
    {
        var ttl = await Raw.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, expected - TimeSpan.FromSeconds(5), expected);
    }
}
