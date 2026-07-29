using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
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

/// <summary>Dedicated fake for Products→Orders revenue lookups — configurable per-product revenue,
/// no query logic (the real aggregate SQL is verified separately via EXPLAIN + manual curl, per this
/// module's own convention of not testing EF query translation without a real DB).</summary>
public sealed class FakeProductRevenueOrderRepository : IOrderRepository
{
    public Dictionary<int, ProductRevenueDto> RevenueByProductId { get; set; } = [];

    public Task AddAsync(CreatorPlatform.Orders.Domain.Orders.Order order, CancellationToken ct) => Task.CompletedTask;

    public Task<CreatorPlatform.Orders.Domain.Orders.Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct)
        => Task.FromResult<CreatorPlatform.Orders.Domain.Orders.Order?>(null);

    public Task<CreatorPlatform.Orders.Domain.Orders.Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct)
        => Task.FromResult<CreatorPlatform.Orders.Domain.Orders.Order?>(null);

    public Task<CreatorPlatform.Orders.Domain.Orders.Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct)
        => Task.FromResult<CreatorPlatform.Orders.Domain.Orders.Order?>(null);

    public Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug, int ownerUserId, DateTimeOffset? afterCreatedAt, Guid? afterId, int limit, CancellationToken ct)
        => Task.FromResult(new List<OrderDto>());

    public Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null));

    public Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null));

    public Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
        => Task.FromResult(new List<PurchasesBucketRow>());

    public Task<HomeSummaryDto> GetHomeSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, [], 0, 0));

    public Task<Dictionary<int, ProductRevenueDto>> GetProductRevenueByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(RevenueByProductId);
}
