namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IAnalyticsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Runs <paramref name="operation"/> in one database transaction: committed when it returns,
    /// rolled back when it throws.</summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct);
}
