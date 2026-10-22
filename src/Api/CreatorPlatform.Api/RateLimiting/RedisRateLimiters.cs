using CreatorPlatform.Shared.Infrastructure.Redis;

namespace CreatorPlatform.Api.RateLimiting;

/// <summary>Creates the Redis-backed limiter of one (policy, partition); the partitioned limiters ask for one the first
/// time they see a partition and keep it until it has been idle for 10 s.</summary>
public sealed class RedisRateLimiters
{
    private readonly IRedisClient _redis;

    public RedisRateLimiters(IRedisClient redis)
    {
        _redis = redis;
    }

    public RedisFixedWindowRateLimiter Create(RateLimitPolicyDefinition policy, string partition) =>
        new(policy, partition, _redis);
}
