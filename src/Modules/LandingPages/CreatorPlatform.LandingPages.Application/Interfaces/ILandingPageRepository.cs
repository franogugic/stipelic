using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public interface ILandingPageRepository
{
    Task<bool> SlugExistsForCreatorAsync(int creatorId, string slug, CancellationToken ct);

    Task AddAsync(LandingPage landingPage, CancellationToken ct);
}
