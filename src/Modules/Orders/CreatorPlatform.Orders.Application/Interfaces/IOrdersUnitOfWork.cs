namespace CreatorPlatform.Orders.Application.Interfaces;

public interface IOrdersUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
