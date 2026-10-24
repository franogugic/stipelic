namespace CreatorPlatform.Shared.Application.Workspaces;

/// <summary>Every file a workspace uploaded (images in the public container, product documents in the private one), by
/// the creator's upload prefixes.</summary>
public interface IWorkspaceFiles
{
    /// <summary>Deletes them all; a missing file is success. Throws when storage isn't configured or a delete fails, so the
    /// caller keeps retrying.</summary>
    Task DeleteAllAsync(int creatorId, CancellationToken ct);
}
