using System.Threading.RateLimiting;

namespace CreatorPlatform.Api.RateLimiting;

/// <summary>
/// <see cref="RateLimitPolicies.CaptureSignupPerEmail"/>: lead sign-ups per email address, counted in Redis across
/// instances and IPs. The address is hashed (<see cref="LoginAttemptPartition.HashEmail"/>), so none appears in a key. A
/// singleton: like the endpoint policies, each partition's limiter is kept while in use.
/// </summary>
public sealed class CaptureEmailRateLimiter : IAsyncDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public CaptureEmailRateLimiter(RedisRateLimiters limiters)
        : this(limiters, RateLimitPolicies.CaptureSignupPerEmail)
    {
    }

    public CaptureEmailRateLimiter(RedisRateLimiters limiters, RateLimitPolicyDefinition policy)
    {
        _limiter = PartitionedRateLimiter.Create<string, string>(email =>
            RateLimitPartition.Get(LoginAttemptPartition.HashEmail(email), hash => limiters.Create(policy, hash)));
    }

    /// <summary>Counts one sign-up for <paramref name="email"/>; the lease says whether it is within the limit.</summary>
    public ValueTask<RateLimitLease> AcquireAsync(string email, CancellationToken ct) => _limiter.AcquireAsync(email, 1, ct);

    public ValueTask DisposeAsync() => _limiter.DisposeAsync();
}
