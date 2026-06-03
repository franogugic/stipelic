using CreatorPlatform.Analytics.Application.Dtos;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IPageViewService
{
    Task RecordAsync(int landingPageId, Guid visitorId, CancellationToken ct);
    Task<LandingPageAnalyticsResponseDto> GetLandingPageStatsAsync(int landingPageId, CancellationToken ct);
}
