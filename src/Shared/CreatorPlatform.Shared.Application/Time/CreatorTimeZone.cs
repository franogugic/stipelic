namespace CreatorPlatform.Shared.Application.Time;

/// <summary>The timezone a creator's analytics are bucketed in: <c>creator_settings.Timezone</c> when it names a
/// real IANA zone, else UTC. Settings only check the shape of the name, so the value is resolved here before it
/// reaches Postgres (<c>AT TIME ZONE</c> fails on an unknown name). Plan-usage periods do not use this — they are
/// billing counters and stay UTC.</summary>
public static class CreatorTimeZone
{
    public const string Fallback = "UTC";

    public static string Resolve(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return Fallback;

        return TimeZoneInfo.TryFindSystemTimeZoneById(timezone.Trim(), out var zone) && zone.HasIanaId
            ? zone.Id
            : Fallback;
    }

    /// <summary>The creator's wall-clock time for <paramref name="now"/>.</summary>
    public static DateTime LocalNow(DateTimeOffset now, string timezone) =>
        TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timezone)).DateTime;

    /// <summary>The instant a local wall-clock time happens in the zone. A time skipped by a DST jump moves forward to
    /// the first valid instant after it.</summary>
    public static DateTimeOffset ToInstant(DateTime local, string timezone)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(unspecified))
            unspecified = unspecified.AddMinutes(30);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, zone), TimeSpan.Zero);
    }

    /// <summary>The first instant of the local calendar month that contains <paramref name="now"/>.</summary>
    public static DateTimeOffset LocalMonthStart(DateTimeOffset now, string timezone)
    {
        var local = LocalNow(now, timezone);
        var start = ToInstant(new DateTime(local.Year, local.Month, 1), timezone);
        return start.ToOffset(TimeZoneInfo.FindSystemTimeZoneById(timezone).GetUtcOffset(start));
    }
}
