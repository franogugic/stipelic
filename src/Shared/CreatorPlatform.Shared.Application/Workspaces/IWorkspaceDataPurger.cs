namespace CreatorPlatform.Shared.Application.Workspaces;

/// <summary>
/// One module's part of erasing a deleted workspace: it deletes (or, where the books need them, anonymises) that
/// module's rows of the creator. Every purger runs in the one transaction of the erasure, in <see cref="Order"/>, after
/// the creator row is locked; each statement is keyed by the creator, so running it again finds nothing.
/// </summary>
public interface IWorkspaceDataPurger
{
    /// <summary>Lower runs first: rows that reference another module's rows go before the rows they reference.</summary>
    int Order { get; }

    Task PurgeAsync(int creatorId, CancellationToken ct);
}

/// <summary>The purgers' places in the erasure, by the foreign keys between their tables.</summary>
public static class WorkspacePurgeOrder
{
    /// <summary>The creator's mail: buyer, lead and campaign emails (bodies hold buyer and contact data).</summary>
    public const int Email = 100;

    /// <summary>Campaigns (recipients, links, clicks), templates, contacts, imports, unsubscribes.</summary>
    public const int Marketing = 200;

    /// <summary>Personal download links and their download events (they reference orders and captures).</summary>
    public const int AccessLinks = 300;

    /// <summary>Page views, lead captures, view totals (they reference the landing pages).</summary>
    public const int Analytics = 400;

    /// <summary>Unpaid orders go; paid and refunded ones stay, anonymised, off the landing pages.</summary>
    public const int Orders = 500;

    public const int LandingPages = 600;

    /// <summary>After the orders and pages that reference them: products without kept orders go, the rest stay as
    /// tombstones.</summary>
    public const int Products = 700;

    public const int MediaReferences = 800;

    /// <summary>Settings, members and usage counters, just before the creator row becomes a tombstone.</summary>
    public const int Creator = 900;
}
