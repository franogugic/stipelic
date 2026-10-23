using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CreatorPlatform.Shared.Infrastructure.Redis;

/// <summary>
/// The read-model caches' storage: JSON strings with a TTL, through <see cref="IRedisClient"/> (key prefix, timeout,
/// circuit breaker). It never throws to the caller: an unavailable Redis is a miss on read and a no-op on write, so the
/// caller reads the database instead. There is deliberately no in-memory fallback: with several API instances it would
/// serve values another instance has already invalidated.
/// </summary>
public sealed class RedisJsonCache
{
    // The API's own JSON shape (camelCase, as the controllers answer), so a cached value reads like a response.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRedisClient _redis;
    private readonly ILogger<RedisJsonCache> _logger;

    public RedisJsonCache(IRedisClient redis, ILogger<RedisJsonCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async ValueTask<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        var read = await _redis.TryExecuteAsync(database => database.StringGetAsync(key), cancellationToken);
        return read.Succeeded ? await DeserializeAsync<T>(key, read.Value, cancellationToken) : null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken) where T : class
    {
        var json = Serialize(value);
        var write = await _redis.TryExecuteAsync(database => database.StringSetAsync(key, json, ttl), cancellationToken);
        if (!write.Succeeded)
            _logger.LogDebug("Redis is unavailable; {Key} was not cached.", key);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var delete = await _redis.TryExecuteAsync(database => database.KeyDeleteAsync(key), cancellationToken);
        if (!delete.Succeeded)
            LogLostInvalidation(key);
    }

    /// <summary>Runs a read the caller builds itself (e.g. a Lua script that resolves the key) and decodes the JSON it
    /// returns, with the same fallback rules as <see cref="GetAsync{T}(string, CancellationToken)"/>. The read reports
    /// the key it actually read, which is dropped if its value no longer deserializes.</summary>
    public async ValueTask<T?> GetAsync<T>(
        Func<IDatabase, Task<(string Key, RedisValue Value)>> read, CancellationToken cancellationToken) where T : class
    {
        var result = await _redis.TryExecuteAsync(read, cancellationToken);
        return result.Succeeded ? await DeserializeAsync<T>(result.Value.Key, result.Value.Value, cancellationToken) : null;
    }

    /// <summary>Runs a write the caller builds itself (a script, a batch); a failure is a no-op, as in
    /// <see cref="SetAsync{T}"/>.</summary>
    public async Task WriteAsync<TResult>(
        string description, Func<IDatabase, Task<TResult>> write, CancellationToken cancellationToken)
    {
        var result = await _redis.TryExecuteAsync(write, cancellationToken);
        if (!result.Succeeded)
            _logger.LogDebug("Redis is unavailable; {Description} was not cached.", description);
    }

    /// <summary>Runs an invalidation the caller builds itself (a batch of deletes, a generation bump). A failure is
    /// logged: the stale entries then live until their TTL.</summary>
    public async Task InvalidateAsync<TResult>(
        string description, Func<IDatabase, Task<TResult>> invalidate, CancellationToken cancellationToken)
    {
        var result = await _redis.TryExecuteAsync(invalidate, cancellationToken);
        if (!result.Succeeded)
            LogLostInvalidation(description);
    }

    public static RedisValue Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private async ValueTask<T?> DeserializeAsync<T>(string key, RedisValue value, CancellationToken cancellationToken)
        where T : class
    {
        if (value.IsNullOrEmpty)
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (JsonException exception)
        {
            // Typically a DTO that changed shape since the value was written: drop it and rebuild from the database.
            _logger.LogWarning(
                "Cached {Key} could not be read as {Type} ({Error}); dropping it.", key, typeof(T).Name, exception.Message);
            await RemoveAsync(key, cancellationToken);
            return null;
        }
    }

    private void LogLostInvalidation(string what) =>
        _logger.LogWarning("Redis is unavailable; {What} was not invalidated and may be served until its TTL ends.", what);
}
