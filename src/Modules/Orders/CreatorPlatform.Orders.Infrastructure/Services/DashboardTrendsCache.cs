using System.Globalization;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Redis;
using StackExchange.Redis;

namespace CreatorPlatform.Orders.Infrastructure.Services;

/// <summary>
/// One entry per (creator, range), all dropped together through a per-creator generation: entries live under
/// <c>dashboard-trends:{id}:g{generation}:{range}</c>, and <see cref="RemoveAsync"/> only increments
/// <c>dashboard-trends:gen:{id}</c> (missing = 0), so old entries are never read again and expire on their TTL. No
/// list of ranges and no key scan is needed, whatever ranges callers use.
/// </summary>
/// <remarks>
/// A read resolves the generation and the entry in one round trip with a Lua script (the entry key depends on the
/// generation, so a pipeline cannot do it). The braces around the creator id are a Redis hash tag: both keys land in
/// the same slot, so the script also runs on a clustered Redis (Azure Managed Redis).
/// </remarks>
public sealed class DashboardTrendsCache : IDashboardTrendsCache
{
    // Short TTL instead of invalidating on every sale: a new sale or view shows up within a minute, and the two
    // aggregate queries behind a miss stay cheap.
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    // Outlives every entry by far, so a generation that expires (and restarts at 0) can never bring back an entry;
    // refreshed on every bump, it keeps the counters of inactive creators from piling up.
    private static readonly TimeSpan GenerationTtl = TimeSpan.FromHours(1);

    // KEYS[1] generation key, KEYS[2] entry key up to the generation number; ARGV[1] range. Returns {generation, value}.
    private const string ReadScript = """
        local generation = redis.call('GET', KEYS[1]) or '0'
        return { generation, redis.call('GET', KEYS[2] .. generation .. ':' .. ARGV[1]) }
        """;

    // As ReadScript; ARGV[2] value, ARGV[3] TTL in milliseconds.
    private const string WriteScript = """
        local generation = redis.call('GET', KEYS[1]) or '0'
        redis.call('SET', KEYS[2] .. generation .. ':' .. ARGV[1], ARGV[2], 'PX', ARGV[3])
        return generation
        """;

    // KEYS[1] generation key; ARGV[1] its TTL in milliseconds.
    private const string BumpScript = """
        local generation = redis.call('INCR', KEYS[1])
        redis.call('PEXPIRE', KEYS[1], ARGV[1])
        return generation
        """;

    private readonly RedisJsonCache _cache;

    public DashboardTrendsCache(RedisJsonCache cache)
    {
        _cache = cache;
    }

    public ValueTask<DashboardTrendsDto?> GetAsync(int creatorId, string range, CancellationToken ct) =>
        _cache.GetAsync<DashboardTrendsDto>(async database =>
        {
            var result = await database.ScriptEvaluateAsync(
                ReadScript, [GenerationKey(creatorId), EntryKeyStem(creatorId)], [range]);
            var parts = (RedisResult[])result!;
            var generation = (string)parts[0]!;
            return (EntryKey(creatorId, generation, range), (RedisValue)parts[1]);
        }, ct);

    public Task SetAsync(int creatorId, string range, DashboardTrendsDto value, CancellationToken ct) =>
        _cache.WriteAsync(
            $"dashboard trends of creator {creatorId}",
            database => database.ScriptEvaluateAsync(
                WriteScript,
                [GenerationKey(creatorId), EntryKeyStem(creatorId)],
                [range, RedisJsonCache.Serialize(value), (long)Ttl.TotalMilliseconds]),
            ct);

    public Task RemoveAsync(int creatorId, CancellationToken ct) =>
        _cache.InvalidateAsync(
            $"dashboard trends of creator {creatorId}",
            database => BumpGenerationAsync(database, creatorId),
            ct);

    /// <summary>Drops every cached range of the creator. Takes an <see cref="IDatabaseAsync"/> so it can join a batch
    /// with other invalidations.</summary>
    public static Task<RedisResult> BumpGenerationAsync(IDatabaseAsync database, int creatorId) =>
        database.ScriptEvaluateAsync(
            BumpScript, [GenerationKey(creatorId)], [(long)GenerationTtl.TotalMilliseconds]);

    public static string GenerationKey(int creatorId) => $"dashboard-trends:gen:{{{creatorId}}}";

    private static string EntryKeyStem(int creatorId) => $"dashboard-trends:{{{creatorId}}}:g";

    private static string EntryKey(int creatorId, string generation, string range) =>
        string.Create(CultureInfo.InvariantCulture, $"{EntryKeyStem(creatorId)}{generation}:{range}");
}
