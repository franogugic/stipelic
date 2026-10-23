using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

public sealed class CampaignLinkRepository : ICampaignLinkRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CampaignLinkRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<CampaignLink> links, CancellationToken ct)
    {
        await _context.Set<CampaignLink>().AddRangeAsync(links, ct);
    }

    public async Task<List<CampaignLink>> GetByCampaignIdAsync(int campaignId, CancellationToken ct)
    {
        return await _context.Set<CampaignLink>()
            .AsNoTracking()
            .Where(l => l.CampaignId == campaignId)
            .OrderBy(l => l.Position)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<int, int>> GetLinkCountsAsync(IReadOnlyCollection<int> campaignIds, CancellationToken ct)
    {
        if (campaignIds.Count == 0)
            return [];

        return await _context.Set<CampaignLink>()
            .AsNoTracking()
            .Where(l => campaignIds.Contains(l.CampaignId))
            .GroupBy(l => l.CampaignId)
            .Select(g => new { CampaignId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CampaignId, x => x.Count, ct);
    }

    public async Task<string?> GetTargetUrlAsync(int recipientId, int linkId, CancellationToken ct)
    {
        return await _context.Database.SqlQuery<string>($"""
            SELECT l."Url" AS "Value"
            FROM marketing.campaign_links l
            JOIN marketing.campaign_recipients r ON r."CampaignId" = l."CampaignId"
            WHERE l."Id" = {linkId} AND r."Id" = {recipientId}
            """)
            .SingleOrDefaultAsync(ct);
    }

    public async Task RecordClickAsync(int recipientId, int linkId, CancellationToken ct)
    {
        // One statement, so concurrent clicks can't double-count: the upsert's conflict on (LinkId, RecipientId)
        // serialises a recipient's clicks on a link, and "xmax = 0" is true only for the row it inserted (the
        // first click); the recipient's "FirstClickedAt IS NULL" update succeeds for one click only. The link
        // must belong to the recipient's campaign, or nothing is written.
        await _context.Database.ExecuteSqlAsync($"""
            WITH link AS (
                SELECT l."Id", l."CampaignId"
                FROM marketing.campaign_links l
                JOIN marketing.campaign_recipients r ON r."CampaignId" = l."CampaignId"
                WHERE l."Id" = {linkId} AND r."Id" = {recipientId}
            ),
            click AS (
                INSERT INTO marketing.campaign_link_clicks AS c ("LinkId", "RecipientId", "FirstClickedAt", "LastClickedAt", "ClickCount")
                SELECT link."Id", {recipientId}, now(), now(), 1 FROM link
                ON CONFLICT ("LinkId", "RecipientId")
                DO UPDATE SET "ClickCount" = c."ClickCount" + 1, "LastClickedAt" = now()
                RETURNING (xmax = 0) AS "FirstOnLink"
            ),
            link_counts AS (
                UPDATE marketing.campaign_links
                SET "TotalClickCount" = "TotalClickCount" + 1,
                    "UniqueClickCount" = "UniqueClickCount" + CASE WHEN (SELECT "FirstOnLink" FROM click) THEN 1 ELSE 0 END
                WHERE "Id" IN (SELECT "Id" FROM link) AND EXISTS (SELECT 1 FROM click)
                RETURNING 1
            ),
            recipient AS (
                UPDATE marketing.campaign_recipients
                SET "FirstClickedAt" = now()
                WHERE "Id" = {recipientId} AND "FirstClickedAt" IS NULL AND EXISTS (SELECT 1 FROM click)
                RETURNING "CampaignId"
            )
            UPDATE marketing.campaigns
            SET "UniqueClickCount" = "UniqueClickCount" + 1
            WHERE "Id" IN (SELECT "CampaignId" FROM recipient)
            """, ct);
    }
}
