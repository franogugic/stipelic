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
}
