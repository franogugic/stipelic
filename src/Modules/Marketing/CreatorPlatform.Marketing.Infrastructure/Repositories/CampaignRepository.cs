using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

public sealed class CampaignRepository : ICampaignRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CampaignRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Campaign campaign, CancellationToken ct)
    {
        await _context.Set<Campaign>().AddAsync(campaign, ct);
    }

    public async Task<Campaign?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct)
    {
        return await _context.Set<Campaign>()
            .FirstOrDefaultAsync(c => c.PublicId == publicId, ct);
    }

    public async Task<Campaign?> GetByPublicIdAsync(Guid publicId, CancellationToken ct)
    {
        return await _context.Set<Campaign>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == publicId, ct);
    }

    public async Task<List<Campaign>> GetRecentByCreatorIdAsync(int creatorId, int take, CancellationToken ct)
    {
        return await _context.Set<Campaign>()
            .AsNoTracking()
            .Where(c => c.CreatorId == creatorId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public void Remove(Campaign campaign)
    {
        _context.Set<Campaign>().Remove(campaign);
    }
}
