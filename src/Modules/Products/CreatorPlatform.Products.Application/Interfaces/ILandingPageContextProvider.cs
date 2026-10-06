using CreatorPlatform.Products.Application.Dtos;

namespace CreatorPlatform.Products.Application.Interfaces;

/// <summary>Products' local read-through to landing pages, so a product can't be pulled from sale while a live page sells it.</summary>
public interface ILandingPageContextProvider
{
    Task<bool> IsUsedByPublishedPageAsync(int productId, CancellationToken ct);

    /// <summary>The product's draft and published pages (archived pages sell nothing), published first.</summary>
    Task<List<ProductSellingPageDto>> GetSellingPagesAsync(int productId, CancellationToken ct);
}
