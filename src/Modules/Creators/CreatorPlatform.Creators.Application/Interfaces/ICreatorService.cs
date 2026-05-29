using CreatorPlatform.Creators.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorService
{
    Task<CreatorResponseDto?> GetCurrentForOwnerAsync(int ownerUserId, CancellationToken ct);

    Task<CreatorSettingsResponseDto> GetSettingsAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<CreatorSettingsResponseDto> UpdateSettingsAsync(
        string slug,
        int ownerUserId,
        UpdateCreatorSettingsRequestDto request,
        CancellationToken ct);

    Task<CreatorResponseDto> CreateAsync(int ownerUserId, CreateCreatorRequestDto request, CancellationToken ct);

    Task DeleteCurrentAsync(int ownerUserId, CancellationToken ct);
}
