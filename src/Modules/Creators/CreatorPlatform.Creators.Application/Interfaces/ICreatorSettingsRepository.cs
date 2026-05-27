using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorSettingsRepository
{
    Task AddAsync(CreatorSettings settings, CancellationToken ct);
}
