using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Email.Infrastructure.Workspaces;

/// <summary>Deletes every outbox row of the workspace, sent or not: buyer, lead and campaign emails all carry the
/// creator. A message being sent right now is skipped by the dispatcher once its row is gone.</summary>
public sealed class EmailWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.Email;

    public async Task PurgeAsync(int creatorId, CancellationToken ct) =>
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM email.email_outbox_messages WHERE "CreatorId" = {creatorId}
            """, ct);
}
