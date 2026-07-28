using CreatorPlatform.Analytics.Application.Dtos;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IViewsSummaryCache
{
    bool TryGet(string creatorSlug, out List<LandingPageViewsSummaryDto>? value);
    void Set(string creatorSlug, List<LandingPageViewsSummaryDto> value);
    void Remove(string creatorSlug);
}
