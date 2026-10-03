using CreatorPlatform.Analytics.Application.Dtos;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IPageViewService
{
    Task RecordAsync(int landingPageId, Guid visitorId, CancellationToken ct);
    Task<LandingPageAnalyticsResponseDto> GetLandingPageStatsAsync(int landingPageId, CancellationToken ct);
    /// <summary>View counts for every landing page of the caller's workspace. 404 when the slug isn't the user's
    /// workspace — checked before the cache is read.</summary>
    Task<List<LandingPageViewsSummaryDto>> GetViewsSummaryByCreatorAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>Drops the cached views summary of the caller's own workspace after its landing pages changed. A slug
    /// that isn't the caller's is ignored. Not cancellable: the change has already been saved.</summary>
    Task InvalidateViewsSummaryAsync(string creatorSlug, int ownerUserId);
}
