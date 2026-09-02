using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Application.Services;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Products;

public class ProductRestoreTests
{
    private const string CreatorSlug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;

    private static (
        ProductService Service,
        FakeProductRepository ProductRepository) BuildService(int maxProducts, int activeProductCount)
    {
        var productRepository = new FakeProductRepository();
        var contextProvider = new FakeProductsCreatorContextProvider
        {
            Context = new CreatorContext(CreatorId, maxProducts, activeProductCount)
        };
        var unitOfWork = new FakeProductsUnitOfWork();
        var orderRepository = new FakeOrderContextProvider();

        var service = new ProductService(productRepository, contextProvider, unitOfWork, orderRepository);

        return (service, productRepository);
    }

    private static Product CreateArchivedProduct()
    {
        var product = Product.Create(CreatorId, "Test Product", null, 1000, ProductType.Digital, null, null, DateTimeOffset.UtcNow);
        product.Archive(DateTimeOffset.UtcNow);
        return product;
    }

    [Fact]
    public async Task RestoreAsync_ArchivedProduct_WithRoomUnderLimit_RestoresToDraft()
    {
        var (service, productRepository) = BuildService(maxProducts: 5, activeProductCount: 1);
        var product = CreateArchivedProduct();
        productRepository.Seed(product);

        var result = await service.RestoreAsync(CreatorSlug, product.PublicId, OwnerUserId, CancellationToken.None);

        Assert.Equal("Draft", result.Status);
        Assert.Equal(ProductStatus.Draft, product.Status);
    }

    [Fact]
    public async Task RestoreAsync_LimitFull_ThrowsConflictAndLeavesStatusUnchanged()
    {
        var (service, productRepository) = BuildService(maxProducts: 1, activeProductCount: 1);
        var product = CreateArchivedProduct();
        productRepository.Seed(product);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.RestoreAsync(CreatorSlug, product.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal(ProductStatus.Archived, product.Status);
    }

    [Fact]
    public async Task RestoreAsync_NonArchivedProduct_Throws()
    {
        var (service, productRepository) = BuildService(maxProducts: 5, activeProductCount: 0);
        var product = Product.Create(CreatorId, "Draft Product", null, 1000, ProductType.Digital, null, null, DateTimeOffset.UtcNow);
        productRepository.Seed(product);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.RestoreAsync(CreatorSlug, product.PublicId, OwnerUserId, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_IncludeArchivedFalse_OmitsArchivedProducts()
    {
        var (service, productRepository) = BuildService(maxProducts: 5, activeProductCount: 1);
        var archived = CreateArchivedProduct();
        var active = Product.Create(CreatorId, "Active Product", null, 1000, ProductType.Digital, null, null, DateTimeOffset.UtcNow);
        productRepository.Seed(archived);
        productRepository.Seed(active);

        var result = await service.ListAsync(CreatorSlug, OwnerUserId, includeArchived: false, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Active Product", result[0].Name);
    }

    [Fact]
    public async Task ListAsync_IncludeArchivedTrue_ReturnsArchivedToo()
    {
        var (service, productRepository) = BuildService(maxProducts: 5, activeProductCount: 1);
        var archived = CreateArchivedProduct();
        var active = Product.Create(CreatorId, "Active Product", null, 1000, ProductType.Digital, null, null, DateTimeOffset.UtcNow);
        productRepository.Seed(archived);
        productRepository.Seed(active);

        var result = await service.ListAsync(CreatorSlug, OwnerUserId, includeArchived: true, CancellationToken.None);

        Assert.Equal(2, result.Count);
    }
}
