using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Workspaces;

/// <summary>Deletes the creator's campaigns (with their recipients, links and clicks), templates, contacts, imports and
/// unsubscribes. We process the creator's contacts for them, so they go at the end of the service (GDPR Art. 28(3)(g));
/// the suppression list goes with the list it suppresses.</summary>
public sealed class MarketingWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.Marketing;

    public async Task PurgeAsync(int creatorId, CancellationToken ct)
    {
        // A non-opener resend references its source campaign (RESTRICT, checked row by row) and must keep it (its audience
        // is that campaign), so the resends go first, the latest round first, then the campaigns. Campaigns reference
        // templates.
        for (var round = Campaign.MaxNonOpenerResends; round >= 1; round--)
            await db.Database.ExecuteSqlAsync($"""
                DELETE FROM marketing.campaigns WHERE "CreatorId" = {creatorId} AND "ResendRound" = {round}
                """, ct);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM marketing.campaigns WHERE "CreatorId" = {creatorId}""", ct);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM marketing.email_templates WHERE "CreatorId" = {creatorId}""", ct);
        // Contacts reference their import (RESTRICT).
        await db.Database.ExecuteSqlAsync($"""DELETE FROM marketing.contact_summaries WHERE "CreatorId" = {creatorId}""", ct);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM marketing.contact_imports WHERE "CreatorId" = {creatorId}""", ct);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM marketing.unsubscribes WHERE "CreatorId" = {creatorId}""", ct);
    }
}
