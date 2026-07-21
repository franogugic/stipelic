namespace CreatorPlatform.Creators.Application.Interfaces;

public enum UsagePeriod
{
    /// <summary>Resets every calendar month, UTC. PeriodStart = first of the month 00:00Z, PeriodEnd =
    /// first of the following month.</summary>
    CalendarMonth,

    /// <summary>Never resets. PeriodStart/End are fixed sentinels (0001-01-01 / 9999-12-31 UTC) so every
    /// call for a given creator/usageKey lands on the same counter row.</summary>
    AllTime
}

public interface ICreatorUsageService
{
    /// <summary>Attempts to consume <paramref name="amount"/> units of <paramref name="usageKey"/> for
    /// the current period. Must be called while the caller already holds a creator-scoped Postgres
    /// advisory lock — there is no additional row-level locking here (the lock IS the concurrency
    /// control; see IMarketingUnitOfWork.AcquireCreatorCampaignLockAsync / the Payouts module's
    /// equivalent). <paramref name="limit"/> &lt; 0 means unlimited: always succeeds (usage is still
    /// recorded for observability). Returns false — with no state changed — if consuming would exceed
    /// the limit. Does not call SaveChanges; the caller's unit of work flushes it.</summary>
    Task<bool> TryConsumeAsync(int creatorId, string usageKey, int amount, int limit, UsagePeriod period, CancellationToken ct);

    /// <summary>Read-only: how much of <paramref name="usageKey"/> has been used in the current period.
    /// Zero if no counter row exists yet (nothing consumed this period).</summary>
    Task<int> GetUsedAsync(int creatorId, string usageKey, UsagePeriod period, CancellationToken ct);

    /// <summary>Gives back <paramref name="amount"/> units previously consumed via
    /// <see cref="TryConsumeAsync"/> — e.g. a send that was counted against the monthly limit but then
    /// permanently failed to deliver. <paramref name="asOf"/> resolves which period row to credit: pass
    /// the timestamp the original consumption happened at (not "now"), since a late-arriving refund must
    /// not credit whatever period happens to be current if it crosses a period boundary (e.g. a month
    /// rollover) after the original send. Floors at zero — never goes negative. A no-op if no counter row
    /// exists for that period (nothing to refund against). Does not call SaveChanges; the caller's unit
    /// of work flushes it.</summary>
    Task RefundAsync(int creatorId, string usageKey, int amount, UsagePeriod period, DateTimeOffset asOf, CancellationToken ct);
}
