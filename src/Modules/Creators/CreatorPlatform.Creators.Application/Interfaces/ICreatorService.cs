using CreatorPlatform.Creators.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorService
{
    Task<CreatorResponseDto?> GetCurrentForOwnerAsync(int ownerUserId, CancellationToken ct);

    Task<CreatorSettingsResponseDto> GetSettingsAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<CreatorResponseDto> CreateAsync(int ownerUserId, CreateCreatorRequestDto request, CancellationToken ct);

    Task DeleteCurrentAsync(int ownerUserId, CancellationToken ct);
}
