using CreatorPlatform.LandingPages.Application.Dtos;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public interface ILandingPageService
{
    Task<LandingPageResponseDto> CreateAsync(
        string slug,
        int ownerUserId,
        CreateLandingPageRequestDto request,
        CancellationToken ct);
}
