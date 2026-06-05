namespace CreatorPlatform.Analytics.Application.Dtos;

public sealed class LandingPageAnalyticsResponseDto
{
    public required PeriodStatsDto AllTime { get; init; }
    public required PeriodStatsDto Today { get; init; }
    public required PeriodStatsDto Last7Days { get; init; }
    public required PeriodStatsDto Last30Days { get; init; }
    public required long TotalEmailCaptures { get; init; }
}

public sealed class PeriodStatsDto
{
    public required long TotalViews { get; init; }
    public required long UniqueVisitors { get; init; }
}
