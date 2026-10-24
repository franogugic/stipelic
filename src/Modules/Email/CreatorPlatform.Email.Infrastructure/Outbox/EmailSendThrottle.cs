using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure.Outbox;

/// <summary>What may be sent right now. <see cref="Campaign"/> leaves the transactional reserve free;
/// <see cref="NextPermitAt"/> is when something may be sent again while <see cref="Transactional"/> is 0.</summary>
public readonly record struct SendBudget(int Transactional, int Campaign, DateTimeOffset? NextPermitAt);

/// <summary>
/// Keeps this Worker instance within the provider's quota: exact sliding windows of one minute and one hour over the sends
/// it started, plus a pause after the provider throttled (429) or was unreachable. Thread-safe; one per process.
/// </summary>
public sealed class EmailSendThrottle
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly object _gate = new();
    private readonly Queue<DateTimeOffset> _lastMinute = new();
    private readonly Queue<DateTimeOffset> _lastHour = new();
    private readonly TimeProvider _timeProvider;
    private readonly int _perMinute;
    private readonly int _perHour;
    private readonly int _minuteReserve;
    private readonly int _hourReserve;
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;

    public EmailSendThrottle(IOptions<EmailOutboxOptions> options, TimeProvider timeProvider)
    {
        var settings = options.Value;
        _timeProvider = timeProvider;
        _perMinute = settings.PerMinute;
        _perHour = settings.PerHour;
        _minuteReserve = Reserve(_perMinute, settings.TransactionalReservePercent);
        _hourReserve = Reserve(_perHour, settings.TransactionalReservePercent);
    }

    public SendBudget GetBudget()
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (now < _pausedUntil)
                return new SendBudget(0, 0, _pausedUntil);

            Trim(now);
            var minuteLeft = _perMinute - _lastMinute.Count;
            var hourLeft = _perHour - _lastHour.Count;
            var transactional = Math.Max(0, Math.Min(minuteLeft, hourLeft));
            var campaign = Math.Max(0, Math.Min(minuteLeft - _minuteReserve, hourLeft - _hourReserve));

            return new SendBudget(transactional, campaign, transactional > 0 ? now : NextPermitAt(minuteLeft, hourLeft));
        }
    }

    /// <summary>Counts <paramref name="count"/> sends starting now.</summary>
    public void Record(int count)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            for (var i = 0; i < count; i++)
            {
                _lastMinute.Enqueue(now);
                _lastHour.Enqueue(now);
            }
        }
    }

    /// <summary>Sends nothing until <paramref name="until"/> (a later pause wins).</summary>
    public void PauseUntil(DateTimeOffset until)
    {
        lock (_gate)
        {
            if (until > _pausedUntil)
                _pausedUntil = until;
        }
    }

    private void Trim(DateTimeOffset now)
    {
        while (_lastMinute.Count > 0 && _lastMinute.Peek() <= now - Minute)
            _lastMinute.Dequeue();
        while (_lastHour.Count > 0 && _lastHour.Peek() <= now - Hour)
            _lastHour.Dequeue();
    }

    // When both windows have room again: the oldest send of each full window leaves it.
    private DateTimeOffset NextPermitAt(int minuteLeft, int hourLeft)
    {
        var minuteFree = minuteLeft > 0 ? DateTimeOffset.MinValue : _lastMinute.Peek() + Minute;
        var hourFree = hourLeft > 0 ? DateTimeOffset.MinValue : _lastHour.Peek() + Hour;
        return minuteFree > hourFree ? minuteFree : hourFree;
    }

    // Campaigns always keep at least one permit per window.
    private static int Reserve(int limit, int percent) =>
        Math.Min((int)Math.Ceiling(limit * percent / 100.0), Math.Max(0, limit - 1));
}
