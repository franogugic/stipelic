using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Shared.Infrastructure.Redis;

/// <summary>
/// Lock-free <see cref="IRedisAvailability"/>. The whole state is one immutable <see cref="OpenCircuit"/> (null while
/// closed) swapped with <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>, so the hot path while Redis is
/// healthy is a single volatile read. Times are <see cref="TimeProvider"/> timestamps (monotonic).
/// </summary>
public sealed class RedisAvailability : IRedisAvailability
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RedisAvailability> _logger;
    private readonly long _breakTicks;
    private readonly int _breakSeconds;

    private OpenCircuit? _circuit;

    public RedisAvailability(IOptions<RedisOptions> options, TimeProvider timeProvider, ILogger<RedisAvailability> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
        _breakSeconds = options.Value.CircuitBreakSeconds;
        _breakTicks = (long)(options.Value.CircuitBreak.TotalSeconds * timeProvider.TimestampFrequency);
    }

    public bool IsAvailable
    {
        get
        {
            var circuit = Volatile.Read(ref _circuit);
            if (circuit is null)
                return true;

            var now = _timeProvider.GetTimestamp();
            if (now < circuit.ProbeAt)
                return false;

            // The break is over: the caller that moves the next probe time forward is the one probe.
            var probing = circuit with { ProbeAt = now + _breakTicks };
            return ReferenceEquals(Interlocked.CompareExchange(ref _circuit, probing, circuit), circuit);
        }
    }

    public void ReportFailure(Exception exception)
    {
        while (true)
        {
            var circuit = Volatile.Read(ref _circuit);
            var now = _timeProvider.GetTimestamp();
            var reopened = circuit is null
                ? new OpenCircuit(OpenedAt: now, ProbeAt: now + _breakTicks)
                : circuit with { ProbeAt = now + _breakTicks };

            if (!ReferenceEquals(Interlocked.CompareExchange(ref _circuit, reopened, circuit), circuit))
                continue;

            // Only the type: a connection exception's message names the host.
            if (circuit is null)
                _logger.LogWarning(
                    "Redis is unavailable ({ExceptionType}). Caches read the database and rate limits count in process; retrying in {BreakSeconds} s.",
                    exception.GetType().Name, _breakSeconds);
            return;
        }
    }

    public void ReportSuccess(long callStartedTimestamp)
    {
        var circuit = Volatile.Read(ref _circuit);
        if (circuit is null || callStartedTimestamp < circuit.OpenedAt)
            return;

        if (ReferenceEquals(Interlocked.CompareExchange(ref _circuit, null, circuit), circuit))
            _logger.LogWarning("Redis is available again.");
    }

    private sealed record OpenCircuit(long OpenedAt, long ProbeAt);
}
