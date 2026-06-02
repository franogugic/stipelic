using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorSettingsRepository
{
    Task AddAsync(CreatorSettings settings, CancellationToken ct);

    Task<CreatorSettings?> GetByCreatorSlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<CreatorSettings?> GetForUpdateBySlugAndOwnerAsync(string slug, int ownerUserId, CancellationToken ct);
}
