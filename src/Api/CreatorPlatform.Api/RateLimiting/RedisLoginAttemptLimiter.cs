using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CreatorPlatform.Api.RateLimiting;

/// <summary>
/// Login attempts counted in Redis, so every API instance shares one count per (client IP, email):
/// <c>login:{ip}:{sha256(email)}</c>, a fixed window that starts with the first attempt. While Redis is unavailable
/// the same rule is counted by <see cref="InMemoryLoginAttemptLimiter"/> (per instance; never "no limit"). Counts
/// from an outage are not carried over when Redis is back.
/// </summary>
public sealed class RedisLoginAttemptLimiter : ILoginAttemptLimiter
{
    // KEYS[1] the window's counter; ARGV[1] the window in milliseconds. The expiry is set with the first attempt (and
    // restored if the key ever lost it), so the window is fixed, not sliding. Returns the count including this attempt.
    private const string AcquireScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 or redis.call('PTTL', KEYS[1]) == -1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return count
        """;

    private readonly IRedisClient _redis;
    private readonly InMemoryLoginAttemptLimiter _fallback;
    private readonly LoginAttemptLimiterOptions _options;

    public RedisLoginAttemptLimiter(
        IRedisClient redis,
        InMemoryLoginAttemptLimiter fallback,
        IOptions<LoginAttemptLimiterOptions> options)
    {
        _redis = redis;
        _fallback = fallback;
        _options = options.Value;
    }

    public async ValueTask<bool> TryAcquireAsync(string ipAddress, string email, CancellationToken cancellationToken)
    {
        var partition = LoginAttemptPartition.For(ipAddress, email);
        var windowMilliseconds = (long)_options.Window.TotalMilliseconds;

        var count = await _redis.TryExecuteAsync(
            async database => (long)await database.ScriptEvaluateAsync(
                AcquireScript, [Key(partition)], [windowMilliseconds]),
            cancellationToken);

        return count.Succeeded
            ? count.Value <= _options.PermitLimit
            : _fallback.TryAcquire(partition);
    }

    public static string Key(string partition) => $"login:{partition}";
}
