using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Application.Services;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.MoneyPath.Tests.Fakes;

namespace CreatorPlatform.MoneyPath.Tests.Products;

public class ProductServiceTests
{
    private const string CreatorSlug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;

    private static (
        ProductService Service,
        FakeProductRepository ProductRepository,
        FakeOrderContextProvider OrderRepository) BuildService()
    {
        var productRepository = new FakeProductRepository();
        var contextProvider = new FakeProductsCreatorContextProvider
        {
            Context = new CreatorContext(CreatorId, MaxProducts: 10, ActiveProductCount: 0)
        };
        var unitOfWork = new FakeProductsUnitOfWork();
        var orderRepository = new FakeOrderContextProvider();

        var service = new ProductService(productRepository, contextProvider, unitOfWork, orderRepository);

        return (service, productRepository, orderRepository);
    }

    private static Product CreateProduct(string name = "Test Product")
        => Product.Create(CreatorId, name, null, 1000, ProductType.Digital, null, null, DateTimeOffset.UtcNow);

    [Fact]
    public async Task ListAsync_ProductWithMultiplePaidOrders_ReturnsExactRevenueSumAndCount()
    {
        var (service, productRepository, orderRepository) = BuildService();
        var product = CreateProduct();
        productRepository.Seed(product);

        orderRepository.RevenueByProductId = new Dictionary<int, ProductRevenueDto>
        {
            [product.Id] = new ProductRevenueDto(product.Id, RevenueCents: 15_00 + 25_00 + 10_00, PaidOrderCount: 3)
        };

        var result = await service.ListAsync(CreatorSlug, OwnerUserId, includeArchived: false, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(5000, dto.RevenueCents);
        Assert.Equal(3, dto.PaidOrderCount);
    }

    [Fact]
    public async Task ListAsync_ProductWithNoOrders_ReturnsZeroNotNullOrError()
    {
        var (service, productRepository, orderRepository) = BuildService();
        var product = CreateProduct();
        productRepository.Seed(product);

        // No entry at all for this product's id — simulates a LEFT JOIN with no matching rows.
        orderRepository.RevenueByProductId = [];

        var result = await service.ListAsync(CreatorSlug, OwnerUserId, includeArchived: false, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(0, dto.RevenueCents);
        Assert.Equal(0, dto.PaidOrderCount);
    }

    [Fact]
    public async Task ListAsync_ProductWithOnlyRefundedOrPendingOrders_ExcludesThemFromRevenue()
    {
        var (service, productRepository, orderRepository) = BuildService();
        var product = CreateProduct();
        productRepository.Seed(product);

        // The repository's aggregate query only sums/counts Status == Paid — a product whose only
        // orders are Refunded/Pending must produce RevenueCents=0, PaidOrderCount=0 from that query.
        // This test documents that contract at the ProductService consumption boundary (the SQL
        // aggregate itself is verified live against real Postgres per this task's acceptance
        // criteria, not via EF InMemory, per this project's established testing convention).
        orderRepository.RevenueByProductId = new Dictionary<int, ProductRevenueDto>
        {
            [product.Id] = new ProductRevenueDto(product.Id, RevenueCents: 0, PaidOrderCount: 0)
        };

        var result = await service.ListAsync(CreatorSlug, OwnerUserId, includeArchived: false, CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(0, dto.RevenueCents);
        Assert.Equal(0, dto.PaidOrderCount);
    }

    [Fact]
    public async Task ListAsync_MultipleProducts_EachGetsItsOwnRevenueByProductId()
    {
        var (service, productRepository, orderRepository) = BuildService();
        var productA = CreateProduct("Product A");
        var productB = CreateProduct("Product B");
        productRepository.Seed(productA);
        productRepository.Seed(productB);

        orderRepository.RevenueByProductId = new Dictionary<int, ProductRevenueDto>
        {
            [productA.Id] = new ProductRevenueDto(productA.Id, RevenueCents: 5000, PaidOrderCount: 2),
            [productB.Id] = new ProductRevenueDto(productB.Id, RevenueCents: 0, PaidOrderCount: 0)
        };

        var result = await service.ListAsync(CreatorSlug, OwnerUserId, includeArchived: false, CancellationToken.None);

        var dtoA = Assert.Single(result, d => d.Name == "Product A");
        var dtoB = Assert.Single(result, d => d.Name == "Product B");
        Assert.Equal(5000, dtoA.RevenueCents);
        Assert.Equal(2, dtoA.PaidOrderCount);
        Assert.Equal(0, dtoB.RevenueCents);
        Assert.Equal(0, dtoB.PaidOrderCount);
    }
}
