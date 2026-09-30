using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

public sealed class CampaignRecipientRepository : ICampaignRecipientRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CampaignRecipientRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<CampaignRecipient> recipients, CancellationToken ct)
    {
        await _context.Set<CampaignRecipient>().AddRangeAsync(recipients, ct);
    }

    public async Task RecordFirstOpenAsync(int recipientId, CancellationToken ct)
    {
        // One statement: the UPDATE ... RETURNING only yields a row for the recipient's first open, so the
        // counter can never be bumped twice for the same recipient, even under concurrent pixel loads.
        await _context.Database.ExecuteSqlAsync($"""
            WITH upd AS (
                UPDATE marketing.campaign_recipients
                SET "FirstOpenedAt" = now()
                WHERE "Id" = {recipientId} AND "FirstOpenedAt" IS NULL
                RETURNING "CampaignId"
            )
            UPDATE marketing.campaigns
            SET "UniqueOpenCount" = "UniqueOpenCount" + 1
            WHERE "Id" IN (SELECT "CampaignId" FROM upd)
            """, ct);
    }
}
