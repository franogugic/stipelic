using CreatorPlatform.Creators.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorPlanService
{
    Task<List<CreatorPlanResponseDto>> ListActiveAsync(CancellationToken ct);
}
