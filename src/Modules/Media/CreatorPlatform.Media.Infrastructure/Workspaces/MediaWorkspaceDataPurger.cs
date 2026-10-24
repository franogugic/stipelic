using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Media.Infrastructure.Workspaces;

/// <summary>Deletes the upload references; the files themselves go after the commit (<see cref="IWorkspaceFiles"/>).</summary>
public sealed class MediaWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.MediaReferences;

    public async Task PurgeAsync(int creatorId, CancellationToken ct) =>
        await db.Database.ExecuteSqlAsync($"""DELETE FROM media.blob_references WHERE "CreatorId" = {creatorId}""", ct);
}
