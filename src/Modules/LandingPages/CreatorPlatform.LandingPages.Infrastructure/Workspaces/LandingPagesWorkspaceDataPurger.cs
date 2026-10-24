using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.LandingPages.Infrastructure.Workspaces;

/// <summary>Deletes the landing pages and, by cascade, their sections.</summary>
public sealed class LandingPagesWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.LandingPages;

    public async Task PurgeAsync(int creatorId, CancellationToken ct) =>
        await db.Database.ExecuteSqlAsync($"""DELETE FROM landing_pages.landing_pages WHERE "CreatorId" = {creatorId}""", ct);
}
