using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public interface ILandingPageSectionRepository
{
    Task<List<LandingPageSection>> ListByLandingPageIdAsync(int landingPageId, CancellationToken ct);

    Task<LandingPageSection?> GetByPublicIdAndLandingPageIdForUpdateAsync(Guid publicId, int landingPageId, CancellationToken ct);

    Task AddAsync(LandingPageSection section, CancellationToken ct);

    void Remove(LandingPageSection section);
}
