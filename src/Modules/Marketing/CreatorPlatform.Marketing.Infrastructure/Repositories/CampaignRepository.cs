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

    public async Task<Campaign?> GetByPublicIdAsync(Guid publicId, CancellationToken ct)
    {
        return await _context.Set<Campaign>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == publicId, ct);
    }

    public async Task<Campaign?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct)
    {
        return await _context.Set<Campaign>()
            .FirstOrDefaultAsync(c => c.PublicId == publicId, ct);
    }

    public async Task LockForUpdateAsync(int campaignId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM marketing.campaigns WHERE \"Id\" = {campaignId} FOR UPDATE", ct);
    }

    public async Task<List<Campaign>> GetResendsAsync(int originalCampaignId, CancellationToken ct)
    {
        return await _context.Set<Campaign>()
            .AsNoTracking()
            .Where(c => c.SourceCampaignId == originalCampaignId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<int, CampaignSourceSummary>> GetSourceSummariesAsync(
        IReadOnlyCollection<int> campaignIds, CancellationToken ct)
    {
        if (campaignIds.Count == 0)
            return [];

        return await _context.Set<Campaign>()
            .AsNoTracking()
            .Where(c => campaignIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new CampaignSourceSummary(c.PublicId, c.Subject), ct);
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

    public async Task<List<Guid>> GetDueScheduledPublicIdsAsync(int limit, CancellationToken ct)
    {
        return await _context.Database.SqlQuery<Guid>($"""
            SELECT "PublicId" AS "Value"
            FROM marketing.campaigns
            WHERE "Status" = 'Scheduled' AND "ScheduledAt" <= now()
            ORDER BY "ScheduledAt"
            LIMIT {limit}
            """)
            .ToListAsync(ct);
    }
}
