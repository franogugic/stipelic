namespace CreatorPlatform.Shared.Application.Analytics;

/// <summary>
/// The lower bounds of the analytics periods, all in UTC: "today" starts at midnight, the last 7 and last 30 days are
/// rolling windows ending now. Built once per request and passed to every source (views, orders, captures) so all
/// numbers on one screen are cut at the same instant.
/// </summary>
public sealed record StatsPeriods(DateTimeOffset StartOfToday, DateTimeOffset Last7DaysFrom, DateTimeOffset Last30DaysFrom)
{
    public static StatsPeriods At(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new StatsPeriods(
            new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero),
            utc.AddDays(-7),
            utc.AddDays(-30));
    }
}
