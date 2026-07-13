using CreatorPlatform.LandingPages.Application.Dtos;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public interface ILandingPageService
{
    Task<LandingPageResponseDto> CreateAsync(
        string creatorSlug,
        int ownerUserId,
        CreateLandingPageRequestDto request,
        CancellationToken ct);

    Task<List<LandingPageResponseDto>> ListAsync(
        string creatorSlug,
        int ownerUserId,
        CancellationToken ct);

    Task<LandingPageWithSectionsResponseDto> GetWithSectionsAsync(
        string creatorSlug,
        Guid landingPagePublicId,
        int ownerUserId,
        CancellationToken ct);

    // Lightweight lookup (no sections, no section repository round trip) for callers that only need the
    // page's metadata/internal id — e.g. analytics, timeseries and captures endpoints.
    Task<LandingPageResponseDto> GetSummaryAsync(
        string creatorSlug,
        Guid landingPagePublicId,
        int ownerUserId,
        CancellationToken ct);

    Task PublishAsync(string creatorSlug, Guid landingPagePublicId, int ownerUserId, CancellationToken ct);

    Task UnpublishAsync(string creatorSlug, Guid landingPagePublicId, int ownerUserId, CancellationToken ct);

    Task ArchiveAsync(string creatorSlug, Guid landingPagePublicId, int ownerUserId, CancellationToken ct);

    Task<LandingPageWithSectionsResponseDto> SaveEditorAsync(
        string creatorSlug,
        Guid landingPagePublicId,
        int ownerUserId,
        SaveLandingPageRequestDto request,
        CancellationToken ct);

    List<SectionTemplateResponseDto> GetSectionTemplates();
}
