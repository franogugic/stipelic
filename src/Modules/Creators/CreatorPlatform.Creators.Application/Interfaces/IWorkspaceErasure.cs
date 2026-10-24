namespace CreatorPlatform.Creators.Application.Interfaces;

public enum WorkspaceErasureOutcome
{
    /// <summary>Data erased, files gone.</summary>
    Done,

    /// <summary>Data erased; deleting the files failed (or storage isn't configured): the maintenance worker retries.</summary>
    FilesPending,

    /// <summary>Nothing erased yet: a buyer just paid one of its Checkout sessions, or a sale started while it was being
    /// deleted. Retried once that order is settled.</summary>
    WaitingForSale,

    /// <summary>Not a disabled workspace: nothing to do.</summary>
    NotDeleted
}

/// <summary>
/// Finishes the deletion of a workspace that is already disabled (billing stopped, unreachable for its owner):
/// <list type="number">
/// <item>its sale Checkout sessions that could still be paid are expired in Stripe;</item>
/// <item>in one transaction, with the creator row locked, every module's purger erases its rows and the creator row
/// becomes a tombstone (<see cref="Domain.Creators.Creator.MarkDeleted"/>);</item>
/// <item>after the commit, its files are deleted from storage.</item>
/// </list>
/// Idempotent: a tombstone only retries its files. A Stripe failure throws before anything is erased.
/// </summary>
public interface IWorkspaceErasure
{
    Task<WorkspaceErasureOutcome> FinishAsync(int creatorId, DateTimeOffset now, CancellationToken ct);
}
