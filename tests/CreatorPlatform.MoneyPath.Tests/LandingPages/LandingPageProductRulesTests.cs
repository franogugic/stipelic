using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Application.Services;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.LandingPages;

/// <summary>F8.1: a Sales page sells a paid product; a lead page gives away an Active free product (its document), or
/// nothing at all (a plain newsletter sign-up).</summary>
public class LandingPageProductRulesTests
{
    private const string CreatorSlug = "acme";
    private const int OwnerUserId = 1;

    private static readonly ProductInfo Paid = new(Guid.NewGuid(), "Presets", 2900, null, IsActive: true);
    private static readonly ProductInfo FreeActive = new(Guid.NewGuid(), "Guide", 0, null, IsActive: true);
    private static readonly ProductInfo FreeDraft = new(Guid.NewGuid(), "Guide", 0, null, IsActive: false);
    private static readonly ProductInfo PaidDraft = new(Guid.NewGuid(), "Presets", 2900, null, IsActive: false);

    private readonly FakeLandingPageRepository _pages = new();
    private readonly FakeLandingPagesCreatorContextProvider _creators = new()
    {
        Context = new CreatorContext(1, 5, 0) { Status = CreatorStatus.Active, PayoutMode = PayoutMode.StripeConnect, StripeConnectPayoutsEnabled = true },
    };
    private readonly LandingPageService _service;

    public LandingPageProductRulesTests()
    {
        _service = new LandingPageService(_pages, new FakeLandingPageSectionRepository(), _creators, new FakeLandingPagesUnitOfWork());
    }

    private Task<LandingPageResponseDto> CreateAsync(string type, ProductInfo? product)
    {
        if (product is not null)
            _creators.Product = product;
        return _service.CreateAsync(CreatorSlug, OwnerUserId,
            new CreateLandingPageRequestDto { Title = "Page", Slug = "page", Type = type, ProductId = product?.PublicId }, CancellationToken.None);
    }

    public static TheoryData<string, string, string> RefusedOnCreate => new()
    {
        { "Sales", "none", LandingPageService.ProductRequiredCode },
        { "Sales", "free", LandingPageService.ProductNotPaidCode },
        { "Sales", "paid-draft", LandingPageService.ProductNotActiveCode },
        { "LeadGen", "paid", LandingPageService.ProductNotFreeCode },
        { "LeadGen", "free-draft", LandingPageService.ProductNotActiveCode },
    };

    private static ProductInfo? Product(string kind) => kind switch
    {
        "paid" => Paid,
        "free" => FreeActive,
        "free-draft" => FreeDraft,
        "paid-draft" => PaidDraft,
        _ => null,
    };

    [Theory]
    [MemberData(nameof(RefusedOnCreate))]
    public async Task APageWithTheWrongKindOfProduct_IsNotCreated(string type, string product, string code)
    {
        var error = await Assert.ThrowsAsync<BadRequestException>(() => CreateAsync(type, Product(product)));

        Assert.Equal(code, error.Code);
    }

    [Theory]
    [InlineData("Sales", "paid")]
    [InlineData("LeadGen", "free")]
    [InlineData("LeadGen", "none")]
    public async Task APageWithTheRightKindOfProduct_OrALeadPageWithNone_IsCreated(string type, string product)
    {
        var page = await CreateAsync(type, Product(product));

        Assert.Equal(type, page.Type);
        Assert.Equal(Product(product)?.PublicId, page.ProductPublicId);
    }

    [Fact]
    public async Task ALeadPageWhoseDocumentIsNoLongerActive_CannotBePublished()
    {
        var page = LandingPage.Create(1, productId: 1, "Guide", "guide", LandingPageType.LeadGen, DateTimeOffset.UtcNow);
        _pages.PageForUpdate = page;
        _creators.Product = FreeDraft;

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.PublishAsync(CreatorSlug, page.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal(LandingPageService.ProductNotActiveCode, error.Code);
        Assert.Equal(LandingPageStatus.Draft, page.Status);
    }

    [Fact]
    public async Task ALeadPageWithoutADocument_CanBePublished()
    {
        var page = LandingPage.Create(1, productId: null, "Newsletter", "news", LandingPageType.LeadGen, DateTimeOffset.UtcNow);
        _pages.PageForUpdate = page;

        await _service.PublishAsync(CreatorSlug, page.PublicId, OwnerUserId, CancellationToken.None);

        Assert.Equal(LandingPageStatus.Published, page.Status);
    }

    [Fact]
    public async Task ASalesPageWithoutAProduct_CannotBePublished()
    {
        var page = LandingPage.Create(1, productId: null, "Sale", "sale", LandingPageType.Sales, DateTimeOffset.UtcNow);
        _pages.PageForUpdate = page;

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.PublishAsync(CreatorSlug, page.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal(LandingPageService.ProductRequiredCode, error.Code);
    }
}
