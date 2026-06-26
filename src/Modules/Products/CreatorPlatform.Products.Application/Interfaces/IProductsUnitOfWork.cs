namespace CreatorPlatform.Products.Application.Interfaces;

public interface IProductsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
