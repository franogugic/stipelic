namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Runs <paramref name="operation"/> in one database transaction: committed when it returns, rolled
    /// back when it throws.</summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}