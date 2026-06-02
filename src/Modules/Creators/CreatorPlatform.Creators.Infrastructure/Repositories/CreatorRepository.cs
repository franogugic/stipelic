using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Creators.Infrastructure.Repositories;

public sealed class CreatorRepository : ICreatorRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<Creator?> GetByOwnerUserIdAsync(int ownerUserId, CancellationToken ct)
    {
        return await _context
            .Set<Creator>()
            .AsNoTracking()
            .FirstOrDefaultAsync(creator =>
                creator.OwnerUserId == ownerUserId
                && creator.Status != CreatorStatus.Disabled,
                ct);
    }

    public async Task<Creator?> GetByIdForUpdateAsync(int id, CancellationToken ct)
    {
        return await _context
            .Set<Creator>()
            .FirstOrDefaultAsync(creator => creator.Id == id, ct);
    }

    public async Task<bool> ExistsByOwnerUserIdAsync(int ownerUserId, CancellationToken ct)
    {
        return await _context
            .Set<Creator>()
            .AsNoTracking()
            .AnyAsync(creator =>
                creator.OwnerUserId == ownerUserId
                && creator.Status != CreatorStatus.Disabled,
                ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        return await _context
            .Set<Creator>()
            .AsNoTracking()
            .AnyAsync(creator =>
                creator.Slug == slug
                && creator.Status != CreatorStatus.Disabled,
                ct);
    }

    public async Task AddAsync(Creator creator, CancellationToken ct)
    {
        await _context.Set<Creator>().AddAsync(creator, ct);
    }

    public async Task<bool> DisableByOwnerUserIdAsync(int ownerUserId, DateTimeOffset disabledAt, CancellationToken ct)
    {
        var updatedCount = await _context
            .Set<Creator>()
            .Where(creator =>
                creator.OwnerUserId == ownerUserId
                && creator.Status != CreatorStatus.Disabled)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(creator => creator.Status, CreatorStatus.Disabled)
                .SetProperty(creator => creator.UpdatedAt, disabledAt),
                ct);

        return updatedCount > 0;
    }
}
