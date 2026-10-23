using System.Diagnostics;
using CreatorPlatform.Analytics.Infrastructure.Services;
using CreatorPlatform.Api.Caching;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Integration.Tests.Redis;

/// <summary>Redis unreachable: every cache call returns within one timeout without throwing (a read is a miss, a write
/// or invalidation does nothing), and the services answer from the database.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ReadModelCacheFallbackTests : IAsyncLifetime
{
    private const int CreatorId = 42;
    private static readonly CancellationToken None = CancellationToken.None;

    // One operation timeout (500 ms) plus room for a slow CI machine.
    private static readonly TimeSpan OneTimeoutWithSlack = TimeSpan.FromMilliseconds(1_500);

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly TestRedisClient _unreachable = RedisFixture.CreateClient(RedisFixture.CreateUnreachableOptions());

    public ReadModelCacheFallbackTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _unreachable.DisposeAsync();

    private static async Task<TimeSpan> TimeAsync(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        return stopwatch.Elapsed;
    }

    [Fact]
    public async Task EveryCacheCall_ReturnsWithinOneTimeout_WithoutThrowing()
    {
        var home = new HomeSummaryCache(_unreachable.JsonCache);
        var views = new ViewsSummaryCache(_unreachable.JsonCache);
        var payouts = new PayoutScheduleCache(_unreachable.JsonCache);
        var invalidator = new CreatorCacheInvalidator(_unreachable.JsonCache);

        // The first call waits out the timeout and opens the circuit; the rest skip Redis.
        var first = await TimeAsync(async () => Assert.Null(await home.GetAsync(CreatorId, None)));
        var rest = await TimeAsync(async () =>
        {
            Assert.Null(await views.GetAsync(CreatorId, None));
            Assert.Null(await payouts.GetAsync(CreatorId, None));
            await home.SetAsync(CreatorId, new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, [], 0, 0, 0, 0, [], [], 0, [], 0, 0, 0), None);
            await views.SetAsync(CreatorId, [], None);
            await payouts.SetAsync(CreatorId, new("daily", 2, null, null), None);
            await home.RemoveAsync(CreatorId, None);
            await views.RemoveAsync(CreatorId, None);
            await invalidator.InvalidateAsync(CreatorId, None);
        });

        Assert.True(first < OneTimeoutWithSlack, $"First call took {first}.");
        Assert.True(rest < TimeSpan.FromMilliseconds(200), $"Calls with the circuit open took {rest}.");
        Assert.Single(_unreachable.AvailabilityLog.Entries, entry => entry.Level == LogLevel.Warning);
        // A lost invalidation is worth a line each: the stale value can live until its TTL.
        Assert.Equal(3, _unreachable.JsonCacheLog.Entries.Count(entry => entry.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task TheHomeSummary_ComesFromTheDatabase()
    {
        var owner = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(owner.CreatorId);
        var home = new HomeSummaryCache(_unreachable.JsonCache);

        await using (var db = _fixture.CreateDbContext())
        {
            var orders = new OrderService(new OrderRepository(db), home);
            Assert.Equal(1, (await orders.GetHomeSummaryAsync(owner.Slug, owner.OwnerUserId, None)).LandingPageCount);
        }

        await _data.CreateLandingPageAsync(owner.CreatorId);

        await using (var db = _fixture.CreateDbContext())
        {
            var orders = new OrderService(new OrderRepository(db), home);
            Assert.Equal(2, (await orders.GetHomeSummaryAsync(owner.Slug, owner.OwnerUserId, None)).LandingPageCount);
        }
    }
}
