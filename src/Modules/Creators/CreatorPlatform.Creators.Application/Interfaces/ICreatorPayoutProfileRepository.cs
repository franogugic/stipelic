using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorPayoutProfileRepository
{
    Task AddAsync(CreatorPayoutProfile profile, CancellationToken ct);

    Task<CreatorPayoutProfile?> GetByCreatorIdAsync(int creatorId, CancellationToken ct);

    Task<CreatorPayoutProfile?> GetForUpdateByCreatorIdAsync(int creatorId, CancellationToken ct);
}
