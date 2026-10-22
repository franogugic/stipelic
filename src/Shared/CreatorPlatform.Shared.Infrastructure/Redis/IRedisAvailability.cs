namespace CreatorPlatform.Shared.Infrastructure.Redis;

/// <summary>
/// The process-wide circuit breaker in front of Redis. After a failed call the circuit opens for
/// <see cref="RedisOptions.CircuitBreak"/>: callers skip Redis (and use their fallback) without waiting on a timeout.
/// When the break is over, one caller probes Redis; its success closes the circuit, its failure opens it again.
/// Each transition is logged once, never per request.
/// </summary>
/// <remarks>Most code goes through <see cref="IRedisClient"/>, which does the checking and reporting.</remarks>
public interface IRedisAvailability
{
    /// <summary>False while the circuit is open. When the break is over, exactly one caller gets true (the probe);
    /// the others keep getting false until the probe reports back or another break has passed.</summary>
    bool IsAvailable { get; }

    /// <summary>A Redis call failed or timed out: open the circuit (again) for one break.</summary>
    void ReportFailure(Exception exception);

    /// <summary>A Redis call succeeded. Closes the circuit, unless the call started before the circuit opened (a slow
    /// success from before the outage proves nothing).</summary>
    /// <param name="callStartedTimestamp">The <see cref="TimeProvider.GetTimestamp"/> taken just before the call.</param>
    void ReportSuccess(long callStartedTimestamp);
}
