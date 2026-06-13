namespace CreatorPlatform.LandingPages.Application.Interfaces;

public sealed record CreatorContext(int CreatorId, int MaxLandingPages, int ActiveLandingPageCount);

public sealed record ProductInfo(string Name, int PriceCents);

public interface ICreatorContextProvider
{
    Task<CreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<int?> GetProductIdForCreatorAsync(int creatorId, Guid productPublicId, CancellationToken ct);

    Task<ProductInfo?> GetProductInfoAsync(int productId, CancellationToken ct);
}
