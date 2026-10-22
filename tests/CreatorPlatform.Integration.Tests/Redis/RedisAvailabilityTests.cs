using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CreatorPlatform.Integration.Tests.Redis;

/// <summary>The circuit breaker on its own, on a manual clock: opens on failure, skips Redis for one break, lets one
/// probe through, closes on its success, and logs each transition exactly once.</summary>
public sealed class RedisAvailabilityTests
{
    private static readonly TimeSpan Break = TimeSpan.FromSeconds(15);

    private readonly ManualTimeProvider _time = new();
    private readonly ListLogger<RedisAvailability> _log = new();
    private readonly RedisAvailability _availability;

    public RedisAvailabilityTests()
    {
        var options = new RedisOptions { CircuitBreakSeconds = (int)Break.TotalSeconds };
        _availability = new RedisAvailability(Options.Create(options), _time, _log);
    }

    private static RedisConnectionException ConnectionFailure() =>
        new(ConnectionFailureType.UnableToConnect, CommandFlags.None, "unreachable", null, CommandStatus.Unknown);

    private List<string> Warnings() =>
        _log.Entries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message).ToList();

    [Fact]
    public void AClosedCircuit_IsAvailable()
    {
        Assert.True(_availability.IsAvailable);
        Assert.True(_availability.IsAvailable);
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void Failures_OpenTheCircuit_AndLogOneWarningWithTheExceptionTypeOnly()
    {
        _availability.ReportFailure(ConnectionFailure());
        _availability.ReportFailure(new RedisTimeoutException(CommandFlags.None, "timed out", CommandStatus.Sent));
        _availability.ReportFailure(ConnectionFailure());

        Assert.False(_availability.IsAvailable);
        var warning = Assert.Single(Warnings());
        Assert.Contains(nameof(RedisConnectionException), warning);
        Assert.DoesNotContain("unreachable", warning);
    }

    [Fact]
    public void WhileOpen_EveryCallerSkips_UntilTheBreakIsOver()
    {
        _availability.ReportFailure(ConnectionFailure());

        _time.Advance(Break - TimeSpan.FromMilliseconds(1));

        Assert.False(_availability.IsAvailable);
        Assert.False(_availability.IsAvailable);
    }

    [Fact]
    public void AfterTheBreak_ExactlyOneCallerProbes()
    {
        _availability.ReportFailure(ConnectionFailure());
        _time.Advance(Break);

        Assert.True(_availability.IsAvailable);
        Assert.False(_availability.IsAvailable);
        Assert.False(_availability.IsAvailable);
    }

    [Fact]
    public void AProbeThatNeverReports_GetsAnotherProbeAfterAnotherBreak()
    {
        _availability.ReportFailure(ConnectionFailure());
        _time.Advance(Break);
        Assert.True(_availability.IsAvailable);

        _time.Advance(Break);

        Assert.True(_availability.IsAvailable);
    }

    [Fact]
    public void AProbeSuccess_ClosesTheCircuit_AndLogsAvailableAgainOnce()
    {
        _availability.ReportFailure(ConnectionFailure());
        _time.Advance(Break);
        Assert.True(_availability.IsAvailable);
        var probeStarted = _time.GetTimestamp();

        _availability.ReportSuccess(probeStarted);
        _availability.ReportSuccess(probeStarted);

        Assert.True(_availability.IsAvailable);
        Assert.True(_availability.IsAvailable);
        Assert.Equal(2, Warnings().Count);
        Assert.Equal("Redis is available again.", Warnings()[1]);
    }

    [Fact]
    public void AProbeFailure_ReopensForAnotherBreak_WithoutAnotherWarning()
    {
        _availability.ReportFailure(ConnectionFailure());
        _time.Advance(Break);
        Assert.True(_availability.IsAvailable);

        _availability.ReportFailure(ConnectionFailure());

        Assert.False(_availability.IsAvailable);
        _time.Advance(Break - TimeSpan.FromMilliseconds(1));
        Assert.False(_availability.IsAvailable);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(_availability.IsAvailable);
        Assert.Single(Warnings());
    }

    [Fact]
    public void ASuccessFromACallStartedBeforeTheOutage_DoesNotCloseTheCircuit()
    {
        var slowCallStarted = _time.GetTimestamp();
        _time.Advance(TimeSpan.FromMilliseconds(100));
        _availability.ReportFailure(ConnectionFailure());

        _availability.ReportSuccess(slowCallStarted);

        Assert.False(_availability.IsAvailable);
        Assert.Single(Warnings());
    }

    [Fact]
    public void ASuccessWhileClosed_ChangesAndLogsNothing()
    {
        _availability.ReportSuccess(_time.GetTimestamp());

        Assert.True(_availability.IsAvailable);
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void ConcurrentFailures_LogOneWarning()
    {
        Parallel.For(0, 1_000, _ => _availability.ReportFailure(ConnectionFailure()));

        Assert.False(_availability.IsAvailable);
        Assert.Single(Warnings());
    }

    [Fact]
    public void ConcurrentCallersAfterTheBreak_GetExactlyOneProbe()
    {
        _availability.ReportFailure(ConnectionFailure());
        _time.Advance(Break);

        var probes = 0;
        Parallel.For(0, 1_000, _ =>
        {
            if (_availability.IsAvailable)
                Interlocked.Increment(ref probes);
        });

        Assert.Equal(1, probes);
    }
}
