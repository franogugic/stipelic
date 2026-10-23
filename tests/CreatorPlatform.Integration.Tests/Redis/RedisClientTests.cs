using System.Diagnostics;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Integration.Tests.Redis;

/// <summary>The client against a real Redis and against an unreachable one: prefixed keys, a failed result (never an
/// exception) within one timeout, no Redis call while the circuit is open, and recovery after the break.</summary>
[Collection(RedisCollection.Name)]
public sealed class RedisClientTests
{
    // One operation timeout (500 ms) plus room for a slow CI machine; far below the client's 5 s connect timeout.
    private static readonly TimeSpan OneTimeoutWithSlack = TimeSpan.FromMilliseconds(1_500);

    private readonly RedisFixture _redis;

    public RedisClientTests(RedisFixture redis)
    {
        _redis = redis;
    }

    [Fact]
    public async Task ACall_RunsAgainstRedis_UnderTheKeyPrefix()
    {
        var options = _redis.CreateOptions();
        await using var redis = RedisFixture.CreateClient(options);

        var set = await redis.Client.TryExecuteAsync(database => database.StringSetAsync("probe", "value"), CancellationToken.None);
        var get = await redis.Client.TryExecuteAsync(database => database.StringGetAsync("probe"), CancellationToken.None);

        Assert.True(set.Succeeded);
        Assert.True(get.Succeeded);
        Assert.Equal("value", get.Value.ToString());
        var raw = _redis.Multiplexer.GetDatabase();
        Assert.Equal("value", (await raw.StringGetAsync($"{options.KeyPrefix}probe")).ToString());
        Assert.False(await raw.KeyExistsAsync("probe"));
        Assert.True(redis.Availability.IsAvailable);
    }

    [Fact]
    public async Task UnreachableRedis_FailsWithinOneTimeout_AndOpensTheCircuit()
    {
        await using var redis = RedisFixture.CreateClient(RedisFixture.CreateUnreachableOptions());

        var stopwatch = Stopwatch.StartNew();
        var result = await redis.Client.TryExecuteAsync(database => database.PingAsync(), CancellationToken.None);
        stopwatch.Stop();

        Assert.False(result.Succeeded);
        Assert.True(stopwatch.Elapsed < OneTimeoutWithSlack, $"Took {stopwatch.Elapsed}.");
        Assert.False(redis.Availability.IsAvailable);
        Assert.Single(redis.AvailabilityLog.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task WhileTheCircuitIsOpen_CallsSkipRedis()
    {
        await using var redis = RedisFixture.CreateClient(RedisFixture.CreateUnreachableOptions());
        await redis.Client.TryExecuteAsync(database => database.PingAsync(), CancellationToken.None);

        var calls = 0;
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            var result = await redis.Client.TryExecuteAsync(database =>
            {
                Interlocked.Increment(ref calls);
                return database.PingAsync();
            }, CancellationToken.None);
            Assert.False(result.Succeeded);
        }
        stopwatch.Stop();

        Assert.Equal(0, calls);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(200), $"Took {stopwatch.Elapsed}.");
        Assert.Single(redis.AvailabilityLog.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task AfterTheBreak_TheFirstSuccessfulCall_ClosesTheCircuit()
    {
        var time = new ManualTimeProvider();
        var options = _redis.CreateOptions();
        await using var redis = RedisFixture.CreateClient(options, time);
        redis.Availability.ReportFailure(new TimeoutException("outage"));

        var skipped = await redis.Client.TryExecuteAsync(database => database.PingAsync(), CancellationToken.None);
        time.Advance(options.CircuitBreak);
        var probe = await redis.Client.TryExecuteAsync(database => database.PingAsync(), CancellationToken.None);

        Assert.False(skipped.Succeeded);
        Assert.True(probe.Succeeded);
        Assert.True(redis.Availability.IsAvailable);
        Assert.Equal(
            ["Redis is available again."],
            redis.AvailabilityLog.Entries.Where(entry => entry.Level == LogLevel.Warning).Skip(1).Select(entry => entry.Message));
    }

    [Fact]
    public async Task Disposing_DoesNotWaitForAConnectToAnUnreachableRedis()
    {
        var redis = RedisFixture.CreateClient(RedisFixture.CreateUnreachableOptions());
        await redis.Client.TryExecuteAsync(database => database.PingAsync(), CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        await redis.DisposeAsync();
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < OneTimeoutWithSlack, $"Took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task ACancelledRequest_Throws_AndDoesNotOpenTheCircuit()
    {
        await using var redis = RedisFixture.CreateClient(_redis.CreateOptions());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            redis.Client.TryExecuteAsync(database => database.PingAsync(), cancellation.Token));

        Assert.True(redis.Availability.IsAvailable);
        Assert.Empty(redis.AvailabilityLog.Entries);
    }
}
