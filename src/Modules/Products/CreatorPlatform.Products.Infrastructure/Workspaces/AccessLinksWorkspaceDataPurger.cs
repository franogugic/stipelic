using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Workspaces;

/// <summary>Deletes the buyers' and leads' personal download links and their download events: the links stop working
/// with the workspace.</summary>
public sealed class AccessLinksWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.AccessLinks;

    public async Task PurgeAsync(int creatorId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"""DELETE FROM products.download_events WHERE "CreatorId" = {creatorId}""", ct);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM products.access_grants WHERE "CreatorId" = {creatorId}""", ct);
    }
}
