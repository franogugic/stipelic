using CreatorPlatform.LandingPages.Application.Dtos;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public interface IPublicLandingPageService
{
    Task<LandingPageWithSectionsResponseDto?> GetPublishedAsync(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct);

    /// <summary>Null for an unknown or Disabled workspace.</summary>
    Task<PublicCreatorPagesResponseDto?> GetCreatorPagesAsync(string creatorSlug, CancellationToken ct);
}
