using CreatorPlatform.Products.Domain.Products;

namespace CreatorPlatform.Products.Application.Interfaces;

public interface IProductRepository
{
    Task<List<Product>> ListByCreatorIdAsync(int creatorId, CancellationToken ct);

    Task<Product?> GetByPublicIdAndCreatorIdForUpdateAsync(Guid publicId, int creatorId, CancellationToken ct);

    Task AddAsync(Product product, CancellationToken ct);
}
