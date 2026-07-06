namespace CreatorPlatform.Orders.Application.Interfaces;

public interface IOrdersUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct);
}
