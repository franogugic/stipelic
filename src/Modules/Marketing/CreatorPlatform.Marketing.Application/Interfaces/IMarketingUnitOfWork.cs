namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IMarketingUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct);

    /// <summary>Postgres transaction-scoped advisory lock keyed on a creator id — serializes concurrent
    /// campaign sends for the same creator (audience resolution + usage-counter consumption must not
    /// race). Released automatically at commit/rollback. Must be called inside
    /// <see cref="ExecuteInTransactionAsync"/>. Uses namespace 42002 — distinct from the Payouts
    /// module's 42001 so the two locks never collide even for the same creator id.</summary>
    Task AcquireCreatorCampaignLockAsync(int creatorId, CancellationToken ct);

    /// <summary>Refreshes a tracked entity's property values from the database — used by the scheduled
    /// dispatch path to re-check a campaign's Status strictly after acquiring the creator lock (a plain
    /// re-query would return the already-tracked instance unchanged, per EF Core's identity map).</summary>
    Task ReloadAsync(object entity, CancellationToken ct);

    /// <summary>Serialises the contact-limit bookkeeping of one contact (sign-up, unsubscribe, delete) until the
    /// transaction ends. Call inside <see cref="ExecuteInTransactionAsync"/>.</summary>
    Task AcquireContactLockAsync(int creatorId, string normalizedEmail, CancellationToken ct);

    /// <summary>A per-creator lock on the contact list as a whole, held until the transaction ends: a contacts import
    /// takes it <paramref name="exclusive"/>, an unsubscribe takes it shared — so an unsubscribe never interleaves
    /// with an import's classify-then-insert, while unsubscribes don't block one another.</summary>
    Task AcquireCreatorContactsLockAsync(int creatorId, bool exclusive, CancellationToken ct);
}
