namespace CreatorPlatform.Payouts.Application.Interfaces;

public interface IPayoutsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct);

    /// <summary>Postgres transaction-scoped advisory lock keyed on a creator id — serializes concurrent
    /// payout creation for the same creator so two simultaneous requests can't both pass the balance
    /// check. Released automatically at commit/rollback. Must be called inside <see cref="ExecuteInTransactionAsync"/>.</summary>
    Task AcquireCreatorPayoutLockAsync(int creatorId, CancellationToken ct);
}
