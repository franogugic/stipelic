using CreatorPlatform.Creators.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorService
{
    Task<CreatorResponseDto> CreateAsync(int ownerUserId, CreateCreatorRequestDto request, CancellationToken ct);
}
