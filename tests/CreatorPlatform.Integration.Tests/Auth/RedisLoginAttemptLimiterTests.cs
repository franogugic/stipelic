using System.Diagnostics;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Auth;

/// <summary>Login attempts counted in a real Redis: 5 per (IP, email) per fixed window, shared by every API instance,
/// keyed without the address, and still limited (in process) while Redis is unreachable.</summary>
[Collection(RedisCollection.Name)]
public sealed class RedisLoginAttemptLimiterTests : IAsyncLifetime
{
    private const string Ip = "203.0.113.7";
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly RedisFixture _redis;
    private readonly string _prefix = RedisFixture.UniqueKeyPrefix();
    private readonly List<TestRedisClient> _clients = [];

    public RedisLoginAttemptLimiterTests(RedisFixture redis)
    {
        _redis = redis;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var client in _clients)
            await client.DisposeAsync();
    }

    /// <summary>One API instance's limiter: its own connection, circuit breaker and in-process fallback.</summary>
    private RedisLoginAttemptLimiter Instance(TimeSpan? window = null, bool unreachable = false)
    {
        var client = RedisFixture.CreateClient(unreachable ? RedisFixture.CreateUnreachableOptions() : _redis.CreateOptions(_prefix));
        _clients.Add(client);
        var options = Options.Create(new LoginAttemptLimiterOptions { Window = window ?? TimeSpan.FromMinutes(10) });
        var fallback = new InMemoryLoginAttemptLimiter(options, TimeProvider.System, new ListLogger<InMemoryLoginAttemptLimiter>());
        return new RedisLoginAttemptLimiter(client.Client, fallback, options);
    }

    private static async Task<List<bool>> AttemptAsync(RedisLoginAttemptLimiter limiter, int times, string email = "ana@example.com")
    {
        var results = new List<bool>();
        for (var i = 0; i < times; i++)
            results.Add(await limiter.TryAcquireAsync(Ip, email, None));
        return results;
    }

    [Fact]
    public async Task TheSixthAttemptInAWindow_IsRefused()
    {
        var limiter = Instance();

        Assert.Equal([true, true, true, true, true, false], await AttemptAsync(limiter, 6));
    }

    [Fact]
    public async Task TheKey_HoldsTheIpAndAHashOfTheEmail_NeverTheAddress_AndExpiresWithTheWindow()
    {
        var limiter = Instance();

        await limiter.TryAcquireAsync(Ip, "  Ana@Example.COM ", None);
        await limiter.TryAcquireAsync(Ip, "ana@example.com", None);

        var keys = _redis.Multiplexer.GetServers().Single().Keys(pattern: $"{_prefix}login:*").Select(key => key.ToString()).ToList();
        var key = Assert.Single(keys);
        Assert.Equal($"{_prefix}login:{Ip}:{LoginAttemptPartition.HashEmail("ana@example.com")}", key);
        Assert.Matches("^[0-9a-f]{32}$", key[(key.LastIndexOf(':') + 1)..]);
        Assert.DoesNotContain("example", key, StringComparison.OrdinalIgnoreCase);
        var database = _redis.Multiplexer.GetDatabase();
        Assert.Equal(2, (long)await database.StringGetAsync(key));
        var ttl = await database.KeyTimeToLiveAsync(key);
        Assert.InRange(ttl!.Value, TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task ANewWindow_AfterTheOldOneExpires_IsAllowedAgain()
    {
        var limiter = Instance(window: TimeSpan.FromMilliseconds(300));
        Assert.False((await AttemptAsync(limiter, 6))[^1]);

        await Task.Delay(TimeSpan.FromMilliseconds(400));

        Assert.True(await limiter.TryAcquireAsync(Ip, "ana@example.com", None));
    }

    [Fact]
    public async Task TheWindowIsFixed_LaterAttemptsDoNotExtendIt()
    {
        var limiter = Instance(window: TimeSpan.FromMilliseconds(600));
        await limiter.TryAcquireAsync(Ip, "ana@example.com", None);

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await AttemptAsync(limiter, 5);
        await Task.Delay(TimeSpan.FromMilliseconds(400));

        Assert.True(await limiter.TryAcquireAsync(Ip, "ana@example.com", None));
    }

    [Fact]
    public async Task TwoInstances_ShareTheCount()
    {
        var a = Instance();
        var b = Instance();

        await AttemptAsync(a, 3);
        await AttemptAsync(b, 2);

        Assert.False(await a.TryAcquireAsync(Ip, "ana@example.com", None));
        Assert.False(await b.TryAcquireAsync(Ip, "ana@example.com", None));
        Assert.True(await b.TryAcquireAsync(Ip, "marko@example.com", None));
    }

    [Fact]
    public async Task RedisUnreachable_TheFallbackStillRefusesTheSixthAttempt()
    {
        var limiter = Instance(unreachable: true);

        var stopwatch = Stopwatch.StartNew();
        var results = await AttemptAsync(limiter, 6);
        stopwatch.Stop();

        Assert.Equal([true, true, true, true, true, false], results);
        // One timeout for the first attempt; the circuit is open for the rest.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(1_500), $"Took {stopwatch.Elapsed}.");
    }
}
