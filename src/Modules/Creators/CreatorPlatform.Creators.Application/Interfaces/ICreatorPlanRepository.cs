using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorPlanRepository
{
    Task<CreatorPlan?> GetByCodeAsync(string code, CancellationToken ct);
}
