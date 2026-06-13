namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record LandingPageProductInfo(
    int CreatorId,
    int ProductId,
    int LandingPageId,
    string ProductName,
    int PriceCents,
    string Currency);

public interface ICreatorContextProvider
{
    Task<LandingPageProductInfo?> GetProductInfoByLandingPageSlugAsync(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct);
}
