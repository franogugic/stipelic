namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>
/// Fixture times on a creator's local days that hold at any hour. The services count up to "now", so a fixture "today at
/// noon" lies in the future every morning and drops out of the period (QA-12). <see cref="Noon"/> keeps local noon for
/// past days (away from a DST change and from midnight) and, for today, falls back to a moment already passed.
/// </summary>
public static class LocalClock
{
    /// <summary>Local noon <paramref name="days"/> local days from today (negative = past), minus
    /// <paramref name="earlierBy"/>. A time not reached yet becomes the latest moment already passed (a second ago,
    /// minus <paramref name="earlierBy"/>), never before today's local midnight — so it stays on today's local day.</summary>
    public static DateTimeOffset Noon(TimeZoneInfo zone, int days, TimeSpan earlierBy = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var noon = Instant(today.AddDays(days).AddHours(12), zone) - earlierBy;
        var latestPassed = now - TimeSpan.FromSeconds(1) - earlierBy;
        if (noon <= latestPassed)
            return noon;

        var midnight = Instant(today, zone);
        return latestPassed < midnight ? midnight : latestPassed;
    }

    private static DateTimeOffset Instant(DateTime local, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone), TimeSpan.Zero);
}
