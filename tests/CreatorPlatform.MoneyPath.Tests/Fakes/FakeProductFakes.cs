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

    public Task<bool> IsFileInUseAsync(string blobPath, int? exceptProductId, CancellationToken ct)
        => Task.FromResult(Products.Any(p => p.FileBlobPath == blobPath && p.Id != exceptProductId));

    /// <summary>Products someone holds a working personal link to.</summary>
    public HashSet<int> ProductsWithActiveGrants { get; } = [];

    public Task<bool> HasActiveAccessGrantsAsync(int productId, CancellationToken ct)
        => Task.FromResult(ProductsWithActiveGrants.Contains(productId));

}

/// <summary>Confirmed uploads by (creator, reference); every other reference resolves to nothing, like another creator's
/// or an unconfirmed upload. Deleted blob paths are recorded.</summary>
public sealed class FakeProductFiles : IProductFiles
{
    public Dictionary<(int CreatorId, Guid Reference), ProductFile> Uploads { get; } = [];
    public List<string> DeletedBlobPaths { get; } = [];

    /// <summary>A confirmed upload of <paramref name="creatorId"/>; returns its reference.</summary>
    public Guid Upload(int creatorId, string fileName = "guide.pdf", long sizeBytes = 8_600_000, string contentType = "application/pdf")
    {
        var reference = Guid.NewGuid();
        Uploads[(creatorId, reference)] = ProductFile.Create(
            $"productfile/{creatorId}/{reference:N}.pdf", fileName, sizeBytes, contentType, DateTimeOffset.UtcNow);
        return reference;
    }

    public Task<ProductFile?> ResolveAsync(int creatorId, Guid fileReference, CancellationToken ct)
        => Task.FromResult(Uploads.GetValueOrDefault((creatorId, fileReference)));

    public Task DeleteAsync(string blobPath, CancellationToken ct)
    {
        DeletedBlobPaths.Add(blobPath);
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

    public Dictionary<int, int[]> TrendByProductId { get; set; } = [];

    public Task<Dictionary<int, int[]>> GetProductSalesTrendsAsync(int creatorId, DateTimeOffset firstMonth, CancellationToken ct)
        => Task.FromResult(TrendByProductId);
}

/// <summary>Products' local landing-page read-through — a single switch for "a published page sells this product".</summary>
public sealed class FakeLandingPageContextProvider : ILandingPageContextProvider
{
    public bool UsedByPublishedPage { get; set; }

    public Task<bool> IsUsedByPublishedPageAsync(int productId, CancellationToken ct)
        => Task.FromResult(UsedByPublishedPage);

    public bool UsedByLeadPage { get; set; }
    public bool UsedBySalesPage { get; set; }

    public Task<(bool LeadPage, bool SalesPage)> GetPageKindsUsingAsync(int productId, CancellationToken ct)
        => Task.FromResult((UsedByLeadPage, UsedBySalesPage));

    public Dictionary<int, ProductSignups> SignupsByProductId { get; } = [];

    public Task<Dictionary<int, ProductSignups>> GetProductSignupsAsync(int creatorId, DateTimeOffset now, CancellationToken ct)
        => Task.FromResult(SignupsByProductId);
}
