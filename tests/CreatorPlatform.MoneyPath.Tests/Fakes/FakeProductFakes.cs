using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Domain.Products;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeProductRepository : IProductRepository
{
    private int _nextId = 1;

    public List<Product> Products { get; } = [];

    /// <summary>Assigns the next auto-increment id (mirrors what a real DB would do on insert) and
    /// adds the product to the in-memory list so <see cref="ListByCreatorIdAsync"/> can return it —
    /// same reflection-based id-assignment pattern as <c>FakePayoutRepository.AddAsync</c>.</summary>
    public void Seed(Product product)
    {
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, _nextId++);
        Products.Add(product);
    }

    public Task<List<Product>> ListByCreatorIdAsync(int creatorId, bool includeArchived, CancellationToken ct)
        => Task.FromResult(Products
            .Where(p => p.CreatorId == creatorId && (includeArchived || p.Status != ProductStatus.Archived))
            .ToList());

    public Task<Product?> GetByPublicIdAndCreatorIdForUpdateAsync(Guid publicId, int creatorId, CancellationToken ct)
        => Task.FromResult(Products.FirstOrDefault(p => p.PublicId == publicId && p.CreatorId == creatorId));

    public Task AddAsync(Product product, CancellationToken ct)
    {
        Seed(product);
        return Task.CompletedTask;
    }
}

public sealed class FakeProductsCreatorContextProvider : CreatorPlatform.Products.Application.Interfaces.ICreatorContextProvider
{
    public CreatorPlatform.Products.Application.Interfaces.CreatorContext? Context { get; set; }

    public Task<CreatorPlatform.Products.Application.Interfaces.CreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(Context);
}

public sealed class FakeProductsUnitOfWork : IProductsUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Dedicated fake for Products' local order-revenue read-through — configurable per-product
/// revenue, no query logic (the real aggregate SQL is verified separately via EXPLAIN + manual curl,
/// per this module's own convention of not testing EF query translation without a real DB).</summary>
public sealed class FakeOrderContextProvider : IOrderContextProvider
{
    public Dictionary<int, ProductRevenueDto> RevenueByProductId { get; set; } = [];

    public Task<Dictionary<int, ProductRevenueDto>> GetProductRevenueByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(RevenueByProductId);
}
