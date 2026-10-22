using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace CreatorPlatform.Shared.Infrastructure.Redis;

/// <summary>
/// The process's single Redis connection (registered as a singleton), created on first use so the API starts while
/// Redis is down. Every call is bounded by the operation timeout, connecting included, and goes through
/// <see cref="IRedisAvailability"/>.
/// </summary>
public sealed class RedisClient : IRedisClient, IAsyncDisposable
{
    private readonly ConfigurationOptions _configuration;
    private readonly RedisKey _keyPrefix;
    private readonly TimeSpan _operationTimeout;
    private readonly IRedisAvailability _availability;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RedisClient> _logger;

    // Taken only to start or restart the connect, never on a call against a live connection.
    private readonly Lock _connectLock = new();
    private Task<Connection>? _connection;
    private bool _disposed;

    public RedisClient(
        IOptions<RedisOptions> options,
        IRedisAvailability availability,
        TimeProvider timeProvider,
        ILogger<RedisClient> logger)
    {
        _configuration = CreateConfiguration(options.Value);
        _keyPrefix = options.Value.KeyPrefix;
        _operationTimeout = options.Value.OperationTimeout;
        _availability = availability;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<RedisCallResult<T>> TryExecuteAsync<T>(
        Func<IDatabase, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_availability.IsAvailable)
            return RedisCallResult<T>.Failed;

        var startedAt = _timeProvider.GetTimestamp();
        var call = ExecuteAsync(operation);
        try
        {
            // The client's own timeouts are checked on a 1 s heartbeat; this bounds the wait exactly.
            var value = await call.WaitAsync(_operationTimeout, cancellationToken).ConfigureAwait(false);
            _availability.ReportSuccess(startedAt);
            return RedisCallResult<T>.Success(value);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            ObserveAbandoned(call);
            _availability.ReportFailure(exception);
            return RedisCallResult<T>.Failed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveAbandoned(call);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task<Connection>? connection;
        lock (_connectLock)
        {
            _disposed = true;
            connection = _connection;
        }

        if (connection is null)
            return;

        // A connect still under way (Redis down) can take the whole connect timeout; shutdown does not wait for it.
        if (!connection.IsCompleted)
        {
            _ = CloseAsync(connection);
            return;
        }

        await CloseAsync(connection).ConfigureAwait(false);
    }

    private async Task CloseAsync(Task<Connection> connection)
    {
        try
        {
            var live = await connection.ConfigureAwait(false);
            await live.Multiplexer.DisposeAsync().ConfigureAwait(false);
        }
        catch (RedisException exception)
        {
            _logger.LogDebug("Redis was never connected; nothing to close ({ExceptionType}).", exception.GetType().Name);
        }
    }

    /// <summary>The client settings on top of the configured connection string: start and keep running while Redis is
    /// down, and fail a call at once while disconnected instead of queueing it until the timeout.</summary>
    public static ConfigurationOptions CreateConfiguration(RedisOptions options)
    {
        var configuration = ConfigurationOptions.Parse(options.ConnectionString);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectRetry = Math.Max(configuration.ConnectRetry, 3);
        configuration.SyncTimeout = options.OperationTimeoutMilliseconds;
        configuration.AsyncTimeout = options.OperationTimeoutMilliseconds;
        configuration.BacklogPolicy = BacklogPolicy.FailFast;
        return configuration;
    }

    private async Task<T> ExecuteAsync<T>(Func<IDatabase, Task<T>> operation)
    {
        var connection = await GetConnectionAsync().ConfigureAwait(false);
        return await operation(connection.Database).ConfigureAwait(false);
    }

    private Task<Connection> GetConnectionAsync()
    {
        var connection = Volatile.Read(ref _connection);
        if (connection is { IsFaulted: false, IsCanceled: false })
            return connection;

        lock (_connectLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_connection is null || _connection.IsFaulted || _connection.IsCanceled)
                Volatile.Write(ref _connection, ConnectAsync());

            return _connection!;
        }
    }

    // With AbortOnConnectFail off, an unreachable server does not throw here: the multiplexer comes back disconnected,
    // calls fail fast, and it keeps reconnecting in the background. Anything that does throw is surfaced as a
    // connection failure so callers fall back instead of failing the request; the next call starts a new connect.
    private async Task<Connection> ConnectAsync()
    {
        try
        {
            var multiplexer = await ConnectionMultiplexer.ConnectAsync(_configuration.Clone()).ConfigureAwait(false);
            return new Connection(multiplexer, multiplexer.GetDatabase().WithKeyPrefix(_keyPrefix));
        }
        catch (Exception exception) when (exception is not RedisException)
        {
            throw new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                CommandFlags.None,
                "Could not create the Redis connection.",
                exception,
                CommandStatus.Unknown);
        }
    }

    // A call abandoned on timeout or cancellation still completes later; read its exception so it never surfaces as
    // an unobserved task exception.
    private static void ObserveAbandoned(Task call) =>
        call.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private sealed record Connection(ConnectionMultiplexer Multiplexer, IDatabase Database);
}
