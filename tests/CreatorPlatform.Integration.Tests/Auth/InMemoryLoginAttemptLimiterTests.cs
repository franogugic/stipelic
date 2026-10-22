using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Auth;

/// <summary>The in-process login limiter used while Redis is down: same fixed-window rule, and bounded memory (expired
/// windows are swept, the number of tracked windows is capped).</summary>
public sealed class InMemoryLoginAttemptLimiterTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ManualTimeProvider _time = new();
    private readonly ListLogger<InMemoryLoginAttemptLimiter> _log = new();

    private InMemoryLoginAttemptLimiter Create(int maxTrackedPartitions = 100_000) =>
        new(Options.Create(new LoginAttemptLimiterOptions { MaxTrackedPartitions = maxTrackedPartitions }), _time, _log);

    private static string Partition(string email = "ana@example.com", string ip = "203.0.113.7") =>
        LoginAttemptPartition.For(ip, email);

    [Fact]
    public void TheSixthAttemptInAWindow_IsRefused()
    {
        var limiter = Create();

        var results = Enumerable.Range(0, 6).Select(_ => limiter.TryAcquire(Partition())).ToList();

        Assert.Equal([true, true, true, true, true, false], results);
    }

    [Fact]
    public void ANewWindow_AfterTheOldOneExpires_IsAllowedAgain()
    {
        var limiter = Create();
        for (var i = 0; i < 6; i++)
            limiter.TryAcquire(Partition());

        _time.Advance(Window);

        Assert.True(limiter.TryAcquire(Partition()));
    }

    [Fact]
    public void PairsAreCountedSeparately()
    {
        var limiter = Create();
        for (var i = 0; i < 5; i++)
            limiter.TryAcquire(Partition());

        Assert.True(limiter.TryAcquire(Partition(email: "marko@example.com")));
        Assert.True(limiter.TryAcquire(Partition(ip: "198.51.100.9")));
        Assert.False(limiter.TryAcquire(Partition()));
    }

    [Fact]
    public void ExpiredWindows_AreEvicted()
    {
        var limiter = Create();
        for (var i = 0; i < 1_000; i++)
            limiter.TryAcquire(Partition(email: $"user{i}@example.com"));
        Assert.Equal(1_000, limiter.TrackedPartitions);

        _time.Advance(Window);
        limiter.TryAcquire(Partition());

        Assert.Equal(1, limiter.TrackedPartitions);
    }

    [Fact]
    public void AtTheCap_NewPairsAreNotTracked_TrackedOnesStillCount_AndItIsLoggedOnce()
    {
        var limiter = Create(maxTrackedPartitions: 3);
        for (var i = 0; i < 3; i++)
            limiter.TryAcquire(Partition(email: $"user{i}@example.com"));

        Assert.True(limiter.TryAcquire(Partition(email: "flood1@example.com")));
        Assert.True(limiter.TryAcquire(Partition(email: "flood2@example.com")));
        for (var i = 0; i < 4; i++)
            limiter.TryAcquire(Partition(email: "user0@example.com"));

        Assert.Equal(3, limiter.TrackedPartitions);
        Assert.False(limiter.TryAcquire(Partition(email: "user0@example.com")));
        Assert.Single(_log.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void AtTheCap_ExpiredWindowsMakeRoom_BeforeThePeriodicSweepIsDue()
    {
        var limiter = Create(maxTrackedPartitions: 3);
        _time.Advance(Window / 2);
        for (var i = 0; i < 3; i++)
            limiter.TryAcquire(Partition(email: $"user{i}@example.com"));
        // The periodic sweep runs here (nothing has expired yet), so the next one is a whole window away.
        _time.Advance(Window / 2);
        limiter.TryAcquire(Partition(email: "user0@example.com"));
        _time.Advance(Window / 2);

        // All three windows have expired, but only the at-the-cap sweep can remove them now.
        for (var i = 0; i < 5; i++)
            Assert.True(limiter.TryAcquire(Partition(email: "new@example.com")));

        Assert.False(limiter.TryAcquire(Partition(email: "new@example.com")));
        Assert.Equal(1, limiter.TrackedPartitions);
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void ConcurrentAttempts_AreAllCounted()
    {
        var limiter = Create();
        var allowed = 0;

        Parallel.For(0, 1_000, _ =>
        {
            if (limiter.TryAcquire(Partition()))
                Interlocked.Increment(ref allowed);
        });

        Assert.Equal(5, allowed);
    }
}
