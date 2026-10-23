using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CreatorPlatform.Api.Health;

/// <summary>A <c>PING</c> through <see cref="IRedisClient"/> (operation timeout, circuit breaker). Redis down is
/// Degraded, never Unhealthy: the API keeps serving from its fallbacks. While the circuit is open nothing is sent;
/// when the break is over this check may be the call that probes Redis and closes it again.</summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IRedisClient _redis;

    public RedisHealthCheck(IRedisClient redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var ping = await _redis.TryExecuteAsync(database => database.PingAsync(), cancellationToken);

        return ping.Succeeded
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded();
    }
}
