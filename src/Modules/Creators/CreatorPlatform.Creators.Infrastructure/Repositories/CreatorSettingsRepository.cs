using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

    public async Task<CreatorSettings?> GetByCreatorSlugForOwnerAsync(
        string slug,
        int ownerUserId,
        CancellationToken ct)
    {
        return await _context
            .Set<CreatorSettings>()
            .AsNoTracking()
            .Include(settings => settings.Creator)
            .FirstOrDefaultAsync(settings =>
                settings.Creator.Slug == slug
                && settings.Creator.OwnerUserId == ownerUserId
                && settings.Creator.Status != CreatorStatus.Disabled,
                ct);
    }
}
