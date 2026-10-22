using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Api.RateLimiting;

/// <summary>
/// The in-process fallback while Redis is unavailable: the same fixed-window rule, counted per API instance.
/// Bounded in memory: expired windows are swept out at most once per window, and at most
/// <see cref="LoginAttemptLimiterOptions.MaxTrackedPartitions"/> are tracked. When that many live windows exist (a
/// flood of distinct emails), a new pair is let through untracked rather than locking real users out; the per-IP
/// <see cref="RateLimitPolicies.Login"/> policy still applies to it.
/// </summary>
public sealed class InMemoryLoginAttemptLimiter
{
    private readonly ConcurrentDictionary<string, AttemptWindow> _windows = new(StringComparer.Ordinal);
    private readonly LoginAttemptLimiterOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemoryLoginAttemptLimiter> _logger;

    private int _count;
    private long _nextSweepTicks;
    private int _fullLogged;

    public InMemoryLoginAttemptLimiter(
        IOptions<LoginAttemptLimiterOptions> options,
        TimeProvider timeProvider,
        ILogger<InMemoryLoginAttemptLimiter> logger)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _nextSweepTicks = timeProvider.GetUtcNow().Add(_options.Window).UtcTicks;
    }

    /// <summary>The (IP, email) windows currently held, expired ones included until the next sweep.</summary>
    public int TrackedPartitions => Volatile.Read(ref _count);

    public bool TryAcquire(string partition)
    {
        var now = _timeProvider.GetUtcNow();
        SweepIfDue(now);

        while (true)
        {
            if (_windows.TryGetValue(partition, out var current))
            {
                var next = current.ExpiresAt <= now
                    ? new AttemptWindow(now + _options.Window, 1)
                    : current with { Count = current.Count + 1 };

                if (_windows.TryUpdate(partition, next, current))
                    return next.Count <= _options.PermitLimit;

                continue;
            }

            if (Volatile.Read(ref _count) >= _options.MaxTrackedPartitions)
            {
                Sweep(now);
                if (Volatile.Read(ref _count) >= _options.MaxTrackedPartitions)
                {
                    if (Interlocked.Exchange(ref _fullLogged, 1) == 0)
                        _logger.LogWarning(
                            "The in-process login limiter tracks {Max} windows; new ones are not counted until some expire.",
                            _options.MaxTrackedPartitions);
                    return true;
                }
            }

            if (_windows.TryAdd(partition, new AttemptWindow(now + _options.Window, 1)))
            {
                Interlocked.Increment(ref _count);
                return 1 <= _options.PermitLimit;
            }
        }
    }

    private void SweepIfDue(DateTimeOffset now)
    {
        var due = Volatile.Read(ref _nextSweepTicks);
        if (now.UtcTicks < due)
            return;

        // One caller sweeps; the others carry on.
        if (Interlocked.CompareExchange(ref _nextSweepTicks, now.Add(_options.Window).UtcTicks, due) == due)
            Sweep(now);
    }

    private void Sweep(DateTimeOffset now)
    {
        foreach (var entry in _windows)
        {
            // Removes the entry only if it is still this expired window (a concurrent attempt may have renewed it).
            if (entry.Value.ExpiresAt <= now && _windows.TryRemove(entry))
                Interlocked.Decrement(ref _count);
        }

        if (Volatile.Read(ref _count) < _options.MaxTrackedPartitions)
            Volatile.Write(ref _fullLogged, 0);
    }

    private sealed record AttemptWindow(DateTimeOffset ExpiresAt, int Count);
}
