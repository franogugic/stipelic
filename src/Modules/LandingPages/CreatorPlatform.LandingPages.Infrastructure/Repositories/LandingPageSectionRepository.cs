using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.LandingPages.Infrastructure.Repositories;

public sealed class LandingPageSectionRepository : ILandingPageSectionRepository
{
    private readonly CreatorPlatformDbContext _context;

    public LandingPageSectionRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<List<LandingPageSection>> ListByLandingPageIdAsync(int landingPageId, CancellationToken ct)
    {
        return await _context
            .Set<LandingPageSection>()
            .Where(s => s.LandingPageId == landingPageId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<LandingPageSection?> GetByPublicIdAndLandingPageIdForUpdateAsync(Guid publicId, int landingPageId, CancellationToken ct)
    {
        return await _context
            .Set<LandingPageSection>()
            .FirstOrDefaultAsync(s => s.PublicId == publicId && s.LandingPageId == landingPageId, ct);
    }

    public async Task AddAsync(LandingPageSection section, CancellationToken ct)
    {
        await _context.Set<LandingPageSection>().AddAsync(section, ct);
    }

    public void Remove(LandingPageSection section)
    {
        _context.Set<LandingPageSection>().Remove(section);
    }
}
