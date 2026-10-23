using CreatorPlatform.Products.Domain.Products;

namespace CreatorPlatform.Products.Application.Interfaces;

public interface IProductRepository
{
    Task<List<Product>> ListByCreatorIdAsync(int creatorId, bool includeArchived, CancellationToken ct);

    Task<Product?> GetByPublicIdAndCreatorIdForUpdateAsync(Guid publicId, int creatorId, CancellationToken ct);

    Task AddAsync(Product product, CancellationToken ct);

    /// <summary>Whether another product (not <paramref name="exceptProductId"/>) already has this document.</summary>
    Task<bool> IsFileInUseAsync(string blobPath, int? exceptProductId, CancellationToken ct);

    /// <summary>Whether someone holds a working personal link to this product (a paid order or a lead sign-up).</summary>
    Task<bool> HasActiveAccessGrantsAsync(int productId, CancellationToken ct);
}
