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

    /// <summary>The first instant of the local calendar month that contains <paramref name="now"/>.</summary>
    public static DateTimeOffset LocalMonthStart(DateTimeOffset now, string timezone)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var start = new DateTime(local.Year, local.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(start, zone.GetUtcOffset(start));
    }
}
