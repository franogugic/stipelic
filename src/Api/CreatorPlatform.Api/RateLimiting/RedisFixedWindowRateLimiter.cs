using System.Diagnostics;
using System.Threading.RateLimiting;
using CreatorPlatform.Shared.Infrastructure.Redis;
using StackExchange.Redis;

namespace CreatorPlatform.Api.RateLimiting;

/// <summary>
/// One (policy, partition) of a catalog policy, counted in Redis so every API instance shares the window.
/// </summary>
/// <remarks>
/// <para><b>Sync vs async.</b> <c>RateLimitingMiddleware</c> first calls <see cref="RateLimiter.AttemptAcquire"/> on the
/// global and the endpoint limiter and only if that fails calls <see cref="RateLimiter.AcquireAsync"/> on both (and
/// calls OnRejected only on the async result). The sync attempt therefore counts nothing and answers "not acquired",
/// which sends the request to the async path, where the one Redis round trip happens. Nothing blocks on Redis.</para>
/// <para><b>Window.</b> Key <c>rl:{policy}:{partition}</c>; the window starts with the partition's first request (the
/// counter's expiry is set then), so it depends only on Redis's clock, never on the instances' clocks, and the remaining
/// TTL is exactly the Retry-After.</para>
/// <para><b>Fallback.</b> When the circuit is open or the call fails, the request is counted by a built-in in-process
/// <see cref="FixedWindowRateLimiter"/> with the same numbers (per instance). It is not auto-replenishing: the owning
/// partitioned limiter's heartbeat calls <see cref="TryReplenish"/>, so no timer per partition.</para>
/// <para><b>Eviction.</b> The partitioned limiter disposes a partition idle for 10 s. Its Redis count lives on in Redis;
/// a partition with fallback counts in its current window reports itself in use, so they are not lost early.</para>
/// </remarks>
public sealed class RedisFixedWindowRateLimiter : ReplenishingRateLimiter
{
    // KEYS[1] the window's counter; ARGV[1] the window in ms, ARGV[2] the permits asked for. The expiry is set on the
    // first hit (and restored if the key ever lost it). Returns {count including this request, remaining ms}.
    private const string AcquireScript = """
        local count = redis.call('INCRBY', KEYS[1], ARGV[2])
        local ttl = redis.call('PTTL', KEYS[1])
        if ttl < 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
            ttl = tonumber(ARGV[1])
        end
        return { count, ttl }
        """;

    private readonly RateLimitPolicyDefinition _policy;
    private readonly string _key;
    private readonly IRedisClient _redis;

    private FixedWindowRateLimiter? _fallback;
    private long _lastUsedTimestamp = Stopwatch.GetTimestamp();
    private int _disposed;

    public RedisFixedWindowRateLimiter(RateLimitPolicyDefinition policy, string partition, IRedisClient redis)
    {
        _policy = policy;
        _key = Key(policy.Name, partition);
        _redis = redis;
    }

    public static string Key(string policyName, string partition) => $"rl:{policyName}:{partition}";

    public override bool IsAutoReplenishing => false;

    public override TimeSpan ReplenishmentPeriod => _policy.Window;

    public override TimeSpan? IdleDuration =>
        Volatile.Read(ref _fallback) is { IdleDuration: null }
            ? null
            : Stopwatch.GetElapsedTime(Volatile.Read(ref _lastUsedTimestamp));

    public override bool TryReplenish() => Volatile.Read(ref _fallback)?.TryReplenish() ?? false;

    public override RateLimiterStatistics? GetStatistics() => Volatile.Read(ref _fallback)?.GetStatistics();

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        ThrowIfInvalid(permitCount);
        // Counting needs Redis, which is async-only here: "not acquired" sends the middleware to AcquireAsync.
        return permitCount == 0 ? Leases.Acquired : Leases.TryAsync;
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        ThrowIfInvalid(permitCount);
        if (permitCount == 0)
            return Leases.Acquired;

        Volatile.Write(ref _lastUsedTimestamp, Stopwatch.GetTimestamp());
        var windowMilliseconds = (long)_policy.Window.TotalMilliseconds;

        var result = await _redis.TryExecuteAsync(
            async database => (RedisResult[])(await database.ScriptEvaluateAsync(
                AcquireScript, [_key], [windowMilliseconds, permitCount]))!,
            cancellationToken);

        Volatile.Write(ref _lastUsedTimestamp, Stopwatch.GetTimestamp());

        if (!result.Succeeded)
            return AcquireFromFallback(permitCount);

        var count = (long)result.Value![0];
        var remaining = TimeSpan.FromMilliseconds((long)result.Value[1]);
        return count <= _policy.PermitLimit ? Leases.Acquired : Leases.Rejected(remaining);
    }

    private RateLimitLease AcquireFromFallback(int permitCount)
    {
        var lease = GetOrCreateFallback().AttemptAcquire(permitCount);
        if (lease.IsAcquired || lease.TryGetMetadata(MetadataName.RetryAfter, out _))
            return lease;

        lease.Dispose();
        return Leases.Rejected(_policy.Window);
    }

    private FixedWindowRateLimiter GetOrCreateFallback()
    {
        if (Volatile.Read(ref _fallback) is { } existing)
            return existing;

        var created = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = _policy.PermitLimit,
            Window = _policy.Window,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = false
        });

        var winner = Interlocked.CompareExchange(ref _fallback, created, null);
        if (winner is null)
            return created;

        created.Dispose();
        return winner;
    }

    private void ThrowIfInvalid(int permitCount)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        ArgumentOutOfRangeException.ThrowIfNegative(permitCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(permitCount, _policy.PermitLimit);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            Volatile.Read(ref _fallback)?.Dispose();

        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        Dispose(disposing: true);
        return ValueTask.CompletedTask;
    }

    private static class Leases
    {
        public static readonly RateLimitLease Acquired = new Lease(true, null);

        // Never reaches OnRejected: the middleware only rejects on the async result.
        public static readonly RateLimitLease TryAsync = new Lease(false, null);

        public static RateLimitLease Rejected(TimeSpan retryAfter) => new Lease(false, retryAfter);
    }

    private sealed class Lease(bool isAcquired, TimeSpan? retryAfter) : RateLimitLease
    {
        private static readonly string[] RetryAfterOnly = [MetadataName.RetryAfter.Name];

        public override bool IsAcquired => isAcquired;

        public override IEnumerable<string> MetadataNames => retryAfter is null ? [] : RetryAfterOnly;

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (retryAfter is { } value && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = value;
                return true;
            }

            metadata = null;
            return false;
        }

        // Static leases are shared, so disposing one must do nothing.
        protected override void Dispose(bool disposing)
        {
        }
    }
}
