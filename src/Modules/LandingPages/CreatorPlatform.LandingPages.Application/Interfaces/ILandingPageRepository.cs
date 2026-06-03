using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public interface ILandingPageRepository
{
    Task<List<LandingPage>> ListByCreatorIdAsync(int creatorId, CancellationToken ct);

    Task<LandingPage?> GetByPublicIdAndCreatorIdForUpdateAsync(Guid publicId, int creatorId, CancellationToken ct);

    Task<bool> SlugExistsForCreatorAsync(int creatorId, string slug, CancellationToken ct);

    Task AddAsync(LandingPage landingPage, CancellationToken ct);
}
