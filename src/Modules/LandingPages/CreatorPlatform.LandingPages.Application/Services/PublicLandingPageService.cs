using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.LandingPages.Application.Services;

public sealed class PublicLandingPageService : IPublicLandingPageService
{
    private readonly ILandingPageRepository _landingPageRepository;
    private readonly ILandingPageSectionRepository _sectionRepository;

    public PublicLandingPageService(
        ILandingPageRepository landingPageRepository,
        ILandingPageSectionRepository sectionRepository)
    {
        _landingPageRepository = landingPageRepository;
        _sectionRepository = sectionRepository;
    }

    public async Task<LandingPageWithSectionsResponseDto?> GetPublishedAsync(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct)
    {
        var landingPage = await _landingPageRepository.GetPublishedBySlugAsync(creatorSlug, landingPageSlug, ct);
        if (landingPage is null)
            return null;

        var sections = await _sectionRepository.ListByLandingPageIdAsync(landingPage.Id, ct);

        return new LandingPageWithSectionsResponseDto
        {
            InternalId = landingPage.Id,
            PublicId = landingPage.PublicId,
            Title = landingPage.Title,
            Slug = landingPage.Slug,
            Type = landingPage.Type.ToString(),
            Status = landingPage.Status.ToString(),
            CustomDomain = landingPage.CustomDomain,
            Sections = sections
                .OrderBy(s => s.SortOrder)
                .Select(s => new LandingPageSectionResponseDto
                {
                    PublicId = s.PublicId,
                    Type = s.Type.ToString(),
                    SortOrder = s.SortOrder,
                    BackgroundColor = s.BackgroundColor,
                    ContentJson = s.ContentJson,
                    IsLocked = s.Type is LandingPageSectionType.Navbar or LandingPageSectionType.Footer
                })
                .ToList(),
            CreatedAt = landingPage.CreatedAt,
            UpdatedAt = landingPage.UpdatedAt
        };
    }
}
