using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorSubscriptionRepository
{
    Task AddAsync(CreatorSubscription subscription, CancellationToken ct);

    /// <summary>The subscription in force: the newest Active / PastDue one, else the newest PendingPayment one (a new
    /// paid workspace that hasn't paid yet). Never a Cancelled one.</summary>
    Task<CreatorSubscription?> GetCurrentByCreatorIdAsync(int creatorId, CancellationToken ct);

    /// <summary>The newest PendingPayment subscription — e.g. an upgrade from Free waiting for payment.</summary>
    Task<CreatorSubscription?> GetPendingByCreatorIdAsync(int creatorId, CancellationToken ct);

    /// <summary>The creator's non-cancelled subscriptions other than <paramref name="excludedSubscriptionId"/>,
    /// tracked — to retire them when that one takes over.</summary>
    Task<IReadOnlyList<CreatorSubscription>> GetOtherCurrentByCreatorIdForUpdateAsync(
        int creatorId, int excludedSubscriptionId, CancellationToken ct);

    Task<CreatorSubscription?> GetByIdForUpdateAsync(int id, CancellationToken ct);

    /// <summary>Takes a row lock (<c>SELECT … FOR UPDATE</c>) on the subscription until the surrounding
    /// transaction ends, so concurrent state changes to it serialize. Must run inside a transaction, before the
    /// row is loaded for update (so the load sees the committed state after any concurrent change).</summary>
    Task LockForUpdateAsync(int id, CancellationToken ct);

    /// <summary>The subscription's committed status, read from the database (not the change tracker) — for re-checking
    /// a row after <see cref="LockForUpdateAsync"/>.</summary>
    Task<CreatorSubscriptionStatus?> GetStatusAsync(int id, CancellationToken ct);

    Task<CreatorSubscription?> GetByProviderSubscriptionIdForUpdateAsync(
        string providerSubscriptionId,
        CancellationToken ct);
}
