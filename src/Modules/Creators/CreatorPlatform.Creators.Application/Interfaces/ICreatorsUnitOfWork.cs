namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct);
}
