using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Creators.Infrastructure.Repositories;

public sealed class CreatorPayoutProfileRepository : ICreatorPayoutProfileRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorPayoutProfileRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CreatorPayoutProfile profile, CancellationToken ct)
    {
        await _context.Set<CreatorPayoutProfile>().AddAsync(profile, ct);
    }

    public async Task<CreatorPayoutProfile?> GetByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await _context
            .Set<CreatorPayoutProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.CreatorId == creatorId, ct);
    }

    public async Task<CreatorPayoutProfile?> GetForUpdateByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await _context
            .Set<CreatorPayoutProfile>()
            .FirstOrDefaultAsync(profile => profile.CreatorId == creatorId, ct);
    }
}
