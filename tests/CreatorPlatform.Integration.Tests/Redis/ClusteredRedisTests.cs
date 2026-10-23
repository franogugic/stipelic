using CreatorPlatform.Analytics.Infrastructure.Services;
using CreatorPlatform.Api.Caching;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CreatorPlatform.Integration.Tests.Redis;

/// <summary>The Redis code on a clustered Redis, where every multi-key command and script must stay in one hash slot:
/// the invalidator's batch (one key per command) runs without CROSSSLOT.</summary>
public sealed class ClusteredRedisTests : IClassFixture<RedisClusterFixture>, IAsyncLifetime
{
    private const int CreatorId = 42;
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly TestRedisClient _redis;

    public ClusteredRedisTests(RedisClusterFixture cluster)
    {
        _redis = RedisFixture.CreateClient(cluster.CreateOptions());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    private static HomeSummaryDto HomeSummary() =>
        new(0, 0, null, 0, 1, [], 0, null, [], 0, 0, 0, 0, [], [], 0, [], 0, 0, 0);

    [Fact]
    public async Task AMultiKeyDeleteAcrossSlots_IsAFailedResult_LoggedAsABug_WithTheCircuitClosed()
    {
        // Also guards the fixture: if this stops failing, the tests below prove nothing.
        var result = await _redis.Client.TryExecuteAsync(
            database => database.KeyDeleteAsync([HomeSummaryCache.Key(CreatorId), ViewsSummaryCache.Key(CreatorId)]),
            None);

        Assert.False(result.Succeeded);
        Assert.Single(_redis.ClientLog.Entries, entry => entry.Level == LogLevel.Error);
        Assert.True(_redis.Availability.IsAvailable);
        Assert.Empty(_redis.AvailabilityLog.Entries);
    }

    [Fact]
    public async Task TheInvalidator_DropsEverySummary_OnACluster()
    {
        var home = new HomeSummaryCache(_redis.JsonCache);
        var views = new ViewsSummaryCache(_redis.JsonCache);
        await home.SetAsync(CreatorId, HomeSummary(), None);
        await views.SetAsync(CreatorId, [new(Guid.NewGuid(), 3, 2)], None);

        await new CreatorCacheInvalidator(_redis.JsonCache).InvalidateAsync(CreatorId, None);

        Assert.Null(await home.GetAsync(CreatorId, None));
        Assert.Null(await views.GetAsync(CreatorId, None));
        Assert.DoesNotContain(_redis.JsonCacheLog.Entries, entry => entry.Level >= LogLevel.Warning);
    }
}
