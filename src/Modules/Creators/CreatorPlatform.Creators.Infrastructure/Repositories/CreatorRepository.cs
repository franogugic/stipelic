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

    public async Task<bool> ExistsByOwnerUserIdAsync(int ownerUserId, CancellationToken ct)
    {
        return await _context
            .Set<Creator>()
            .AsNoTracking()
            .AnyAsync(creator => creator.OwnerUserId == ownerUserId, ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        return await _context
            .Set<Creator>()
            .AsNoTracking()
            .AnyAsync(creator => creator.Slug == slug, ct);
    }

    public async Task AddAsync(Creator creator, CancellationToken ct)
    {
        await _context.Set<Creator>().AddAsync(creator, ct);
    }
}
