using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Application.Services;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Products;

public class ProductStatusUpdateTests
{
    private const string CreatorSlug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;

    private static (
        ProductService Service,
        FakeProductRepository ProductRepository,
        FakeLandingPageContextProvider LandingPages) BuildService()
    {
        var productRepository = new FakeProductRepository();
        var contextProvider = new FakeProductsCreatorContextProvider
        {
            Context = new CreatorContext(CreatorId, MaxProducts: 10, ActiveProductCount: 0)
        };
        var landingPages = new FakeLandingPageContextProvider();
        var service = new ProductService(
            productRepository, contextProvider, new FakeProductsUnitOfWork(), new FakeOrderContextProvider(), landingPages, new FakeAnalyticsContextProvider());
        return (service, productRepository, landingPages);
    }

    private static Product SeedProduct(FakeProductRepository repository, ProductStatus status)
    {
        var product = Product.Create(CreatorId, "Test Product", null, 1000, ProductType.Digital, null, null, DateTimeOffset.UtcNow);
        if (status == ProductStatus.Active) product.Publish(DateTimeOffset.UtcNow);
        repository.Seed(product);
        return product;
    }

    private static UpdateProductRequestDto UpdateRequest(string? status) => new()
    {
        Name = "Renamed",
        PriceCents = 2900,
        Type = "Digital",
        Status = status
    };

    [Fact]
    public async Task UpdateAsync_DraftToActive_Publishes()
    {
        var (service, repository, _) = BuildService();
        var product = SeedProduct(repository, ProductStatus.Draft);

        var result = await service.UpdateAsync(CreatorSlug, product.PublicId, OwnerUserId, UpdateRequest("Active"), CancellationToken.None);

        Assert.Equal("Active", result.Status);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public async Task UpdateAsync_ActiveToDraft_Unpublishes_WhenNoPublishedPageUsesIt()
    {
        var (service, repository, _) = BuildService();
        var product = SeedProduct(repository, ProductStatus.Active);

        var result = await service.UpdateAsync(CreatorSlug, product.PublicId, OwnerUserId, UpdateRequest("draft"), CancellationToken.None);

        Assert.Equal("Draft", result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ActiveToDraft_WhilePublishedPageUsesIt_ThrowsConflictAndChangesNothing()
    {
        var (service, repository, landingPages) = BuildService();
        var product = SeedProduct(repository, ProductStatus.Active);
        landingPages.UsedByPublishedPage = true;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(CreatorSlug, product.PublicId, OwnerUserId, UpdateRequest("Draft"), CancellationToken.None));

        Assert.Equal("This product is used by a published page.", ex.Message);
        Assert.Equal(ProductStatus.Active, product.Status);
        Assert.Equal("Test Product", product.Name);
    }

    [Fact]
    public async Task UpdateAsync_ArchivedStatus_IsRejectedWithAHintToTheArchiveEndpoint()
    {
        var (service, repository, _) = BuildService();
        var product = SeedProduct(repository, ProductStatus.Active);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(CreatorSlug, product.PublicId, OwnerUserId, UpdateRequest("Archived"), CancellationToken.None));

        Assert.Contains("archive endpoint", ex.Message);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task UpdateAsync_MissingStatus_KeepsTheCurrentOne(string? status)
    {
        var (service, repository, landingPages) = BuildService();
        var active = SeedProduct(repository, ProductStatus.Active);
        var draft = SeedProduct(repository, ProductStatus.Draft);
        landingPages.UsedByPublishedPage = true; // must not matter: the status is not changing

        var activeResult = await service.UpdateAsync(CreatorSlug, active.PublicId, OwnerUserId, UpdateRequest(status), CancellationToken.None);
        var draftResult = await service.UpdateAsync(CreatorSlug, draft.PublicId, OwnerUserId, UpdateRequest(status), CancellationToken.None);

        Assert.Equal("Active", activeResult.Status);
        Assert.Equal("Draft", draftResult.Status);
        Assert.Equal("Renamed", activeResult.Name);
    }

    [Fact]
    public async Task UpdateAsync_UnknownStatus_IsRejected()
    {
        var (service, repository, _) = BuildService();
        var product = SeedProduct(repository, ProductStatus.Draft);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateAsync(CreatorSlug, product.PublicId, OwnerUserId, UpdateRequest("Banana"), CancellationToken.None));
    }

    [Theory]
    [InlineData("Active", "Active")]
    [InlineData("Draft", "Draft")]
    [InlineData(null, "Draft")]
    public async Task CreateAsync_UsesTheRequestedStatus_DefaultingToDraft(string? status, string expected)
    {
        var (service, _, _) = BuildService();

        var result = await service.CreateAsync(CreatorSlug, OwnerUserId,
            new CreateProductRequestDto { Name = "New", PriceCents = 1000, Type = "Digital", Status = status }, CancellationToken.None);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task CreateAsync_ArchivedStatus_IsRejected()
    {
        var (service, _, _) = BuildService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateAsync(CreatorSlug, OwnerUserId,
                new CreateProductRequestDto { Name = "New", PriceCents = 1000, Type = "Digital", Status = "Archived" }, CancellationToken.None));
    }
}
