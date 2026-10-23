using System.Threading.RateLimiting;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Integration.Tests.Infrastructure;

namespace CreatorPlatform.Integration.Tests.Redis;

/// <summary>One (policy, partition) limiter on its own: the sync attempt never counts (the middleware then goes async),
/// and while it holds in-process fallback counts it reports itself in use, so the partitioned limiter cannot evict it
/// (and with it the counts) before its window is over.</summary>
public sealed class RedisFixedWindowRateLimiterTests : IAsyncLifetime
{
    private static readonly RateLimitPolicyDefinition Policy = new("Test", 2, TimeSpan.FromMilliseconds(300));

    private readonly TestRedisClient _unreachable = RedisFixture.CreateClient(RedisFixture.CreateUnreachableOptions());

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _unreachable.DisposeAsync();

    [Fact]
    public void TheSyncAttempt_IsNeverAcquired_AndCountsNothing()
    {
        using var limiter = new RedisFixedWindowRateLimiter(Policy, "203.0.113.7", _unreachable.Client);

        for (var i = 0; i < 10; i++)
        {
            using var lease = limiter.AttemptAcquire();
            Assert.False(lease.IsAcquired);
            Assert.False(lease.TryGetMetadata(MetadataName.RetryAfter, out _));
        }

        Assert.Null(limiter.GetStatistics());
    }

    [Fact]
    public async Task FallbackCounts_KeepThePartitionInUse_UntilTheWindowIsReplenished()
    {
        using var limiter = new RedisFixedWindowRateLimiter(Policy, "203.0.113.7", _unreachable.Client);

        using (var first = await limiter.AcquireAsync())
            Assert.True(first.IsAcquired);
        using (var second = await limiter.AcquireAsync())
            Assert.True(second.IsAcquired);
        using (var third = await limiter.AcquireAsync())
        {
            Assert.False(third.IsAcquired);
            Assert.True(third.TryGetMetadata(MetadataName.RetryAfter, out _));
        }

        Assert.Null(limiter.IdleDuration);

        await Task.Delay(Policy.Window + TimeSpan.FromMilliseconds(100));
        // The partitioned limiter's heartbeat does this every 100 ms.
        Assert.True(limiter.TryReplenish());

        Assert.NotNull(limiter.IdleDuration);
        using var afterWindow = await limiter.AcquireAsync();
        Assert.True(afterWindow.IsAcquired);
    }

    [Fact]
    public async Task ThePartitionedLimiter_EvictsAnIdlePartition()
    {
        var created = 0;
        using var partitioned = PartitionedRateLimiter.Create<string, string>(partition =>
            RateLimitPartition.Get(partition, key =>
            {
                Interlocked.Increment(ref created);
                return new RedisFixedWindowRateLimiter(Policy, key, _unreachable.Client);
            }));

        using (var lease = await partitioned.AcquireAsync("203.0.113.7"))
            Assert.True(lease.IsAcquired);

        // Window (300 ms) + the 10 s idle limit + heartbeat slack.
        await Task.Delay(TimeSpan.FromSeconds(12));
        using (var lease = await partitioned.AcquireAsync("203.0.113.7"))
            Assert.True(lease.IsAcquired);

        Assert.Equal(2, created);
    }
}
