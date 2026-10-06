using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.LandingPages.Application.Services;

public sealed class PublicLandingPageService : IPublicLandingPageService
{
    private const int MaxCreatorPages = 6;

    private readonly ILandingPageRepository _landingPageRepository;
    private readonly ILandingPageSectionRepository _sectionRepository;
    private readonly ICreatorContextProvider _creatorContextProvider;

    public PublicLandingPageService(
        ILandingPageRepository landingPageRepository,
        ILandingPageSectionRepository sectionRepository,
        ICreatorContextProvider creatorContextProvider)
    {
        _landingPageRepository = landingPageRepository;
        _sectionRepository = sectionRepository;
        _creatorContextProvider = creatorContextProvider;
    }

    public async Task<LandingPageWithSectionsResponseDto?> GetPublishedAsync(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct)
    {
        var landingPage = await _landingPageRepository.GetPublishedBySlugAsync(creatorSlug, landingPageSlug, ct);
        if (landingPage is null)
            return null;

        var sections = await _sectionRepository.ListByLandingPageIdAsync(landingPage.Id, ct);

        var productInfo = landingPage.ProductId.HasValue
            ? await _creatorContextProvider.GetProductInfoAsync(landingPage.ProductId.Value, ct)
            : null;

        // The page was just found through its creator, so the brand is there.
        var brand = await _creatorContextProvider.GetPublicBrandAsync(landingPage.CreatorId, ct)
            ?? throw new InvalidOperationException($"Creator {landingPage.CreatorId} of a published page is missing.");

        return new LandingPageWithSectionsResponseDto
        {
            Id = landingPage.Id,
            CreatorId = landingPage.CreatorId,
            ProductId = landingPage.ProductId,
            ProductPublicId = productInfo?.PublicId,
            ProductName = productInfo?.Name,
            ProductThumbnailUrl = productInfo?.ThumbnailUrl,
            ProductPriceCents = productInfo?.PriceCents,
            PublicId = landingPage.PublicId,
            Title = landingPage.Title,
            Slug = landingPage.Slug,
            Type = landingPage.Type.ToString(),
            Status = landingPage.Status.ToString(),
            CustomDomain = landingPage.CustomDomain,
            Creator = ToDto(brand),
            Product = productInfo is null ? null : new PublicProductDto(productInfo.Type, brand.Currency),
            Sections = sections
                .OrderBy(s => s.SortOrder)
                .Select(s => new LandingPageSectionResponseDto
                {
                    PublicId = s.PublicId,
                    Type = s.Type.ToString(),
                    Variant = s.Variant,
                    SortOrder = s.SortOrder,
                    BackgroundColor = s.BackgroundColor,
                    ContentJson = s.ContentJson,
                    IsLocked = s.Type is LandingPageSectionType.Navbar or LandingPageSectionType.Footer
                })
                .ToList(),
            CreatedAt = landingPage.CreatedAt,
            UpdatedAt = landingPage.UpdatedAt
        };
    }

    public async Task<PublicCreatorPagesResponseDto?> GetCreatorPagesAsync(string creatorSlug, CancellationToken ct)
    {
        var brand = await _creatorContextProvider.GetPublicBrandBySlugAsync(creatorSlug, ct);
        if (brand is null)
            return null;

        var pages = await _landingPageRepository.ListPublishedByCreatorIdAsync(brand.CreatorId, MaxCreatorPages, ct);
        var products = await _creatorContextProvider.GetProductInfosAsync(
            pages.Where(p => p.ProductId.HasValue).Select(p => p.ProductId!.Value).Distinct().ToList(), ct);

        return new PublicCreatorPagesResponseDto(
            ToDto(brand),
            pages.Select(page =>
            {
                var product = page.ProductId is int id ? products.GetValueOrDefault(id) : null;
                return new PublicCreatorPageDto(
                    page.Title,
                    page.Slug,
                    page.Type.ToString(),
                    product?.PriceCents,
                    product is null ? null : brand.Currency,
                    product?.ThumbnailUrl);
            }).ToList());
    }

    private static PublicCreatorBrandDto ToDto(PublicCreatorBrand brand) => new(brand.Name, brand.PrimaryColor, brand.LogoUrl);
}
