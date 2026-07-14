namespace CreatorPlatform.Analytics.Application.Dtos;

public sealed class LandingPageAnalyticsResponseDto
{
    // Page header fields — merged in here so the analytics view needs one request instead of a
    // separate lightweight summary call just to render the title/slug/status.
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required string Status { get; init; }
    public required PeriodStatsDto AllTime { get; init; }
    public required PeriodStatsDto Today { get; init; }
    public required PeriodStatsDto Last7Days { get; init; }
    public required PeriodStatsDto Last30Days { get; init; }
    public required long TotalEmailCaptures { get; init; }
    public required int PurchaseCount { get; init; }
    public required int TotalRevenueCents { get; init; }
    public string? Currency { get; init; }
}

public sealed class PeriodStatsDto
{
    public required long TotalViews { get; init; }
    public required long UniqueVisitors { get; init; }
}
