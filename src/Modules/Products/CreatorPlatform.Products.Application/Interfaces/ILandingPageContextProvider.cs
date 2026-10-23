using CreatorPlatform.Products.Application.Dtos;

namespace CreatorPlatform.Products.Application.Interfaces;

/// <summary>Products' local read-through to landing pages, so a product can't be pulled from sale while a live page sells it.</summary>
public interface ILandingPageContextProvider
{
    Task<bool> IsUsedByPublishedPageAsync(int productId, CancellationToken ct);

    /// <summary>Whether a draft or published (not archived) page uses the product, by page kind: a lead page gives a
    /// free product away, a Sales page sells a paid one.</summary>
    Task<(bool LeadPage, bool SalesPage)> GetPageKindsUsingAsync(int productId, CancellationToken ct);

    /// <summary>Sign-ups on the creator's lead pages, by the product each page gives away, in one aggregate query: all time,
    /// and per calendar month in the creator's timezone for the <see cref="ProductSalesTrend.Months"/> months up to the one
    /// containing <paramref name="now"/>. Products without sign-ups are absent.</summary>
    Task<Dictionary<int, ProductSignups>> GetProductSignupsAsync(int creatorId, DateTimeOffset now, CancellationToken ct);
}
