namespace CreatorPlatform.Creators.Application.Interfaces;

/// <summary>Pure period-boundary calculation for <see cref="ICreatorUsageService"/> — extracted so it's
/// unit-testable without a database (the "new month = new counter row" behavior of the real service
/// follows directly from two different `now` instants resolving to different period boundaries here).</summary>
public static class UsagePeriodResolver
{
    // Zero-fractional-second sentinels — safe from timestamptz round-trip precision drift.
    public static readonly DateTimeOffset AllTimeStart = new(1, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset AllTimeEnd = new(9999, 12, 31, 0, 0, 0, TimeSpan.Zero);

    public static (DateTimeOffset Start, DateTimeOffset End) Resolve(UsagePeriod period, DateTimeOffset now)
    {
        if (period == UsagePeriod.AllTime)
            return (AllTimeStart, AllTimeEnd);

        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddMonths(1));
    }
}
