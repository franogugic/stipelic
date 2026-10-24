using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Analytics.Infrastructure.Workspaces;

/// <summary>Deletes the page views (visitor ids), the lead captures (lead emails) and the view totals.</summary>
public sealed class AnalyticsWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.Analytics;

    public async Task PurgeAsync(int creatorId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM analytics.page_views
            WHERE "LandingPageId" IN (SELECT "Id" FROM landing_pages.landing_pages WHERE "CreatorId" = {creatorId})
            """, ct);
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM analytics.email_captures
            WHERE "LandingPageId" IN (SELECT "Id" FROM landing_pages.landing_pages WHERE "CreatorId" = {creatorId})
               OR "ProductId" IN (SELECT "Id" FROM products.products WHERE "CreatorId" = {creatorId})
            """, ct);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM analytics.creator_view_totals WHERE "CreatorId" = {creatorId}""", ct);
    }
}
