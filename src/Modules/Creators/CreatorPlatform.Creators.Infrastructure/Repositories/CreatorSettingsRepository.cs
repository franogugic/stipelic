using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Creators.Infrastructure.Repositories;

public sealed class CreatorSettingsRepository : ICreatorSettingsRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorSettingsRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CreatorSettings settings, CancellationToken ct)
    {
        await _context.Set<CreatorSettings>().AddAsync(settings, ct);
    }
}
