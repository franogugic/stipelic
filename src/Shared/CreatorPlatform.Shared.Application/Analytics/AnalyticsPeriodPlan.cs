namespace CreatorPlatform.Shared.Application.Analytics;

/// <summary>One period turned into UTC ranges and local bucket starts, for the creator's timezone. Pure: the
/// analytics, orders and contacts reads take the numbers for exactly these ranges and fill these buckets. Hours, days
/// and months are calendar buckets; the long periods (2m / 3m / 6m) use 7-day buckets anchored at the window's first
/// day, the last one ending now.</summary>
public sealed record AnalyticsPeriodPlan(
    string Period,
    string Step,
    // The totals range (From null = all time).
    DateTimeOffset? TotalsFrom,
    DateTimeOffset TotalsTo,
    // The shown range (for "all": since → now).
    DateTimeOffset RangeFrom,
    DateTimeOffset RangeTo,
    // The span the buckets cover (current and previous), in UTC.
    DateTimeOffset BucketsFrom,
    DateTimeOffset BucketsTo,
    IReadOnlyList<DateTime> Buckets,
    DateTimeOffset? PreviousTotalsFrom,
    DateTimeOffset? PreviousTotalsTo,
    DateTimeOffset? PreviousRangeTo,
    IReadOnlyList<DateTime>? PreviousBuckets)
{
    public const int ChartMonths = 12;

    public static readonly IReadOnlyList<string> Periods = ["today", "7d", "15d", "30d", "2m", "3m", "6m", "all"];

    private const int WeekDays = 7;

    private static readonly IReadOnlyDictionary<string, int> DayPeriods = new Dictionary<string, int>
    {
        ["7d"] = 7,
        ["15d"] = 15,
        ["30d"] = 30,
    };

    // 2 / 3 / 6 months as whole local days; the buckets are weeks.
    private static readonly IReadOnlyDictionary<string, int> WeekPeriods = new Dictionary<string, int>
    {
        ["2m"] = 60,
        ["3m"] = 90,
        ["6m"] = 182,
    };

    private static readonly DateTime EpochOrigin = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>Every bucket start the reads must tell apart, oldest first: the previous period's buckets, then the
    /// current period's. A bucket ends where the next one starts, so a read bins each event with one
    /// <c>width_bucket</c> over <see cref="Edges"/> instead of truncating dates per step.</summary>
    public IReadOnlyList<DateTime> AllBucketStarts => [.. PreviousBuckets ?? [], .. Buckets];

    /// <summary><see cref="AllBucketStarts"/> as seconds since 1970-01-01 of the local wall-clock time — the same scale
    /// as <c>extract(epoch from (instant AT TIME ZONE zone))</c>, which makes the bin independent of DST jumps.</summary>
    public double[] Edges => ToEdges(AllBucketStarts);

    public static double[] ToEdges(IReadOnlyList<DateTime> starts) =>
        [.. starts.Select(start => (DateTime.SpecifyKind(start, DateTimeKind.Unspecified) - EpochOrigin).TotalSeconds)];

    /// <param name="since">When the page / product was created; "all" starts there.</param>
    public static AnalyticsPeriodPlan Create(string period, DateTimeOffset now, string timezone, DateTimeOffset since)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var local = TimeZoneInfo.ConvertTime(now, zone).DateTime;
        var today = local.Date;

        switch (period)
        {
            case "today":
            {
                var yesterday = today.AddDays(-1);
                var hours = Enumerable.Range(0, local.Hour + 1).Select(h => today.AddHours(h)).ToList();
                var previousHours = Enumerable.Range(0, 24).Select(h => yesterday.AddHours(h)).ToList();
                var from = ToUtc(today, zone);
                var previousFrom = ToUtc(yesterday, zone);
                // Yesterday up to the same local time, so "today so far" compares like with like.
                var previousCut = ToUtc(local.AddDays(-1), zone);
                return new AnalyticsPeriodPlan(
                    period, "hour", from, now, from, now, previousFrom, now, hours,
                    previousFrom, previousCut, from, previousHours);
            }
            case "7d":
            case "15d":
            case "30d":
            {
                var days = DayPeriods[period];
                var first = today.AddDays(-(days - 1));
                var previousFirst = first.AddDays(-days);
                var current = Enumerable.Range(0, days).Select(d => first.AddDays(d)).ToList();
                var previous = Enumerable.Range(0, days).Select(d => previousFirst.AddDays(d)).ToList();
                var from = ToUtc(first, zone);
                var previousFrom = ToUtc(previousFirst, zone);
                return new AnalyticsPeriodPlan(
                    period, "day", from, now, from, now, previousFrom, now, current,
                    previousFrom, from, from, previous);
            }
            case "2m":
            case "3m":
            case "6m":
            {
                var days = WeekPeriods[period];
                var first = today.AddDays(-(days - 1));
                var previousFirst = first.AddDays(-days);
                // Weeks anchored at each window's own first day; the last one is partial (up to now / up to the
                // current window's start). Local calendar arithmetic, so a DST change never shifts a bucket.
                var current = Weeks(first, days);
                var previous = Weeks(previousFirst, days);
                var from = ToUtc(first, zone);
                var previousFrom = ToUtc(previousFirst, zone);
                return new AnalyticsPeriodPlan(
                    period, "week", from, now, from, now, previousFrom, now, current,
                    previousFrom, from, from, previous);
            }
            case "all":
            {
                var thisMonth = new DateTime(local.Year, local.Month, 1);
                var localSince = TimeZoneInfo.ConvertTime(since, zone).DateTime;
                var sinceMonth = new DateTime(localSince.Year, localSince.Month, 1);
                var firstMonth = thisMonth.AddMonths(-(ChartMonths - 1));
                if (sinceMonth > firstMonth)
                    firstMonth = sinceMonth > thisMonth ? thisMonth : sinceMonth;
                var months = new List<DateTime>();
                for (var month = firstMonth; month <= thisMonth; month = month.AddMonths(1))
                    months.Add(month);
                return new AnalyticsPeriodPlan(
                    period, "month", null, now, since, now, ToUtc(firstMonth, zone), now, months,
                    null, null, null, null);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown period.");
        }
    }

    private static List<DateTime> Weeks(DateTime first, int days) =>
        Enumerable.Range(0, (days + WeekDays - 1) / WeekDays).Select(week => first.AddDays(week * WeekDays)).ToList();

    /// <summary>A local wall-clock time as an instant. A time skipped by a DST jump (e.g. 02:30 on the spring change)
    /// moves forward to the first valid instant after it.</summary>
    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(unspecified))
            unspecified = unspecified.AddMinutes(30);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, zone), TimeSpan.Zero);
    }
}
