using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Creators.Infrastructure.Workspaces;

/// <summary>Deletes the brand settings (name, support email, logo), the members and the usage counters. The
/// subscriptions stay: they are the platform's billing history.</summary>
public sealed class CreatorWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.Creator;

    public async Task PurgeAsync(int creatorId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"""DELETE FROM creators.creator_settings WHERE "CreatorId" = {creatorId}""", ct);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM creators.creator_members WHERE "CreatorId" = {creatorId}""", ct);
        await db.Database.ExecuteSqlAsync($"""DELETE FROM creators.creator_usage_counters WHERE "CreatorId" = {creatorId}""", ct);
    }
}
