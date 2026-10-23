namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IAnalyticsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Runs <paramref name="operation"/> in one database transaction: committed when it returns,
    /// rolled back when it throws.</summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct);

    /// <summary>Serialises the contact-limit bookkeeping of one contact (sign-up, unsubscribe, delete) until the
    /// transaction ends. Call inside <see cref="ExecuteInTransactionAsync"/>.</summary>
    Task AcquireContactLockAsync(int creatorId, string normalizedEmail, CancellationToken ct);
}
