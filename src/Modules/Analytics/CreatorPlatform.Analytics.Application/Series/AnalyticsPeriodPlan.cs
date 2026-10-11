namespace CreatorPlatform.Analytics.Application.Series;

/// <summary>One period turned into UTC ranges and local bucket starts, for the creator's timezone. Pure: the
/// series service reads the numbers for exactly these ranges and fills these buckets.</summary>
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

    public static readonly IReadOnlyList<string> Periods = ["today", "7d", "30d", "all"];

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
            case "30d":
            {
                var days = period == "7d" ? 7 : 30;
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
