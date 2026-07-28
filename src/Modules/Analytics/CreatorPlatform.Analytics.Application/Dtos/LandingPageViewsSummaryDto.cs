namespace CreatorPlatform.Analytics.Application.Dtos;

public sealed record LandingPageViewsSummaryDto(
    Guid PublicId,
    long TotalViews,
    long UniqueVisitors);
