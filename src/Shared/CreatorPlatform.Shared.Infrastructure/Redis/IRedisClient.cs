using StackExchange.Redis;

namespace CreatorPlatform.Shared.Infrastructure.Redis;

/// <summary>
/// The one way into Redis. Owns the process's single connection and runs each call inside the circuit breaker and
/// the operation timeout, so a caller never waits longer than one timeout and never sees a Redis exception: it gets
/// a failed result and uses its fallback.
/// </summary>
public interface IRedisClient
{
    /// <summary>
    /// Runs <paramref name="operation"/> against a database whose keys all get the configured key prefix. The
    /// operation uses the async commands only (batches and transactions included); a sync command would block a thread.
    /// Returns a failed result, without calling Redis, while the circuit is open; a failed result (reported to
    /// <see cref="IRedisAvailability"/>) when the call fails or exceeds the operation timeout; and a failed result
    /// (logged as an error, circuit untouched) for a command the client refuses to send. Only cancellation of
    /// <paramref name="cancellationToken"/> is thrown.
    /// </summary>
    Task<RedisCallResult<T>> TryExecuteAsync<T>(Func<IDatabase, Task<T>> operation, CancellationToken cancellationToken);
}

/// <summary>The outcome of <see cref="IRedisClient.TryExecuteAsync{T}"/>: the value when Redis answered, else a failure
/// (circuit open, error or timeout) the caller treats as "use the fallback".</summary>
public readonly record struct RedisCallResult<T>(bool Succeeded, T? Value)
{
    public static RedisCallResult<T> Failed => default;

    public static RedisCallResult<T> Success(T value) => new(true, value);
}
