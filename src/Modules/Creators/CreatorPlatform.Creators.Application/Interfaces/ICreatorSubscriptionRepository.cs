using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorSubscriptionRepository
{
    Task AddAsync(CreatorSubscription subscription, CancellationToken ct);

    Task<CreatorSubscription?> GetCurrentByCreatorIdAsync(int creatorId, CancellationToken ct);

    Task<CreatorSubscription?> GetByIdForUpdateAsync(int id, CancellationToken ct);

    /// <summary>Takes a row lock (<c>SELECT … FOR UPDATE</c>) on the subscription until the surrounding
    /// transaction ends, so concurrent state changes to it serialize. Must run inside a transaction, before the
    /// row is loaded for update (so the load sees the committed state after any concurrent change).</summary>
    Task LockForUpdateAsync(int id, CancellationToken ct);

    Task<CreatorSubscription?> GetByProviderSubscriptionIdForUpdateAsync(
        string providerSubscriptionId,
        CancellationToken ct);
}
