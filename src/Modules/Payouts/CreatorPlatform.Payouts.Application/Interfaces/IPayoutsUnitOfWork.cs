namespace CreatorPlatform.Payouts.Application.Interfaces;

public interface IPayoutsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct);
}
