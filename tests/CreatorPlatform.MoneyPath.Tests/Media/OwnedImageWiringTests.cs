using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Application.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Application.Media;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.MoneyPath.Tests.Media;

/// <summary>S3 addition: a product's thumbnail and a new workspace's logo go through the owned-image check (with the
/// creator, or with none for a workspace not created yet), and a refusal saves nothing.</summary>
public class OwnedImageWiringTests
{
    private const string Thumbnail = "http://127.0.0.1:10000/devstoreaccount1/creator-media/ProductThumbnail/1/x.png";

    private static (ProductService Service, FakeProductRepository Products, FakeOwnedImageCheck Images) Products()
    {
        var products = new FakeProductRepository();
        var images = new FakeOwnedImageCheck();
        var service = new ProductService(
            products,
            new FakeProductsCreatorContextProvider { Context = new CreatorContext(7, MaxProducts: 10, ActiveProductCount: 0) },
            new FakeProductsUnitOfWork(), new FakeOrderContextProvider(), new FakeLandingPageContextProvider(), new FakeProductFiles(), images);
        return (service, products, images);
    }

    [Fact]
    public async Task AProductsThumbnail_IsCheckedAsTheCreatorsImage()
    {
        var (service, products, images) = Products();

        await service.CreateAsync("acme", 1, new CreateProductRequestDto { Name = "Guide", PriceCents = 0, ThumbnailUrl = $"  {Thumbnail} " }, default);

        var call = Assert.Single(images.Calls);
        Assert.Equal((7, Thumbnail), (call.CreatorId, call.Urls.Single()));
        Assert.Single(products.Products);
    }

    [Fact]
    public async Task ARefusedThumbnail_SavesNoProduct()
    {
        var (service, products, images) = Products();
        images.Refuse = true;

        var refused = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateAsync("acme", 1, new CreateProductRequestDto { Name = "Guide", PriceCents = 0, ThumbnailUrl = Thumbnail }, default));

        Assert.Equal(IOwnedImageCheck.ImageNotUploadedCode, refused.Code);
        Assert.Empty(products.Products);
    }

    [Fact]
    public async Task ANewWorkspacesLogo_IsCheckedWithNoCreator_SoNoneOfOurUploadsPasses()
    {
        var images = new FakeOwnedImageCheck { Refuse = true };
        var creators = new FakeCreatorRepository();
        var plans = new FakeCreatorPlanRepository();
        plans.PlansByCode["free"] = CreatorPlan.Create("free", "Free", null, 0, Currency.Eur, BillingInterval.None, 1000, null, DateTimeOffset.UtcNow);
        var service = new CreatorService(
            creators, new FakeCreatorMemberRepository(), plans, new FakeCreatorSettingsRepository(), new FakeCreatorSubscriptionRepository(), new FakeCreatorsUnitOfWork(), new FakeSubscriptionCheckoutSessionService(),
            new FakeSubscriptionCancellationService(), new FakeBillingPortalService(),             new FakeBillingCustomerService(), new FakeCreatorCacheInvalidator(), NullLogger<CreatorService>.Instance, images);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(1, new CreateCreatorRequestDto
        {
            Name = "Acme", Slug = "acme", PlanCode = "free", DefaultCurrency = "EUR", CountryCode = "HR", LogoUrl = Thumbnail
        }, default));

        var call = Assert.Single(images.Calls);
        Assert.Equal((null, Thumbnail), (call.CreatorId, call.Urls.Single()));
        Assert.Empty(creators.Added);
    }
}
