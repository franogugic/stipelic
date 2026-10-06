using CreatorPlatform.Analytics.Application.Dtos;

namespace CreatorPlatform.Analytics.Application.Interfaces;

/// <summary>Short-lived cache of per-landing-page view counts per creator. Read or written only after the caller's
/// ownership of the creator was checked — the key is the internal creator id, never anything the caller supplies.</summary>
public interface IViewsSummaryCache
{
    bool TryGet(int creatorId, out List<LandingPageViewsSummaryDto>? value);
    void Set(int creatorId, List<LandingPageViewsSummaryDto> value);
    void Remove(int creatorId);
}
