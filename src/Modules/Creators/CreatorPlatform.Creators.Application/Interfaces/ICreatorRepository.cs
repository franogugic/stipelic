using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorRepository
{
    Task<Creator?> GetByOwnerUserIdAsync(int ownerUserId, CancellationToken ct);

    Task<bool> ExistsByOwnerUserIdAsync(int ownerUserId, CancellationToken ct);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);

    Task AddAsync(Creator creator, CancellationToken ct);
}
