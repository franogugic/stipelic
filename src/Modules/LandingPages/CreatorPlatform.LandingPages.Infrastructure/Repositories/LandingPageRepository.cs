using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.LandingPages.Infrastructure.Repositories;

public sealed class LandingPageRepository : ILandingPageRepository
{
    private readonly CreatorPlatformDbContext _context;

    public LandingPageRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<List<LandingPage>> ListByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await _context
            .Set<LandingPage>()
            .AsNoTracking()
            .Where(lp => lp.CreatorId == creatorId && lp.Status != LandingPageStatus.Archived)
            .OrderByDescending(lp => lp.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<LandingPage?> GetByPublicIdAndCreatorIdForUpdateAsync(Guid publicId, int creatorId, CancellationToken ct)
    {
        return await _context
            .Set<LandingPage>()
            .FirstOrDefaultAsync(lp => lp.PublicId == publicId && lp.CreatorId == creatorId, ct);
    }

    public async Task<bool> SlugExistsForCreatorAsync(int creatorId, string slug, CancellationToken ct)
    {
        return await _context
            .Set<LandingPage>()
            .AsNoTracking()
            .AnyAsync(lp =>
                lp.CreatorId == creatorId
                && lp.Slug == slug
                && lp.Status != LandingPageStatus.Archived,
                ct);
    }

    public async Task AddAsync(LandingPage landingPage, CancellationToken ct)
    {
        await _context.Set<LandingPage>().AddAsync(landingPage, ct);
    }
}
