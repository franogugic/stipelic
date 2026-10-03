using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public sealed record CreatorContext(int CreatorId, int MaxLandingPages, int ActiveLandingPageCount)
{
    public CreatorStatus Status { get; init; }
    public PayoutMode PayoutMode { get; init; }
    public bool StripeConnectPayoutsEnabled { get; init; }
    public bool HasPayoutProfile { get; init; }
}

public sealed record ProductInfo(Guid PublicId, string Name, int PriceCents, string? ThumbnailUrl);

public interface ICreatorContextProvider
{
    Task<CreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<int?> GetProductIdForCreatorAsync(int creatorId, Guid productPublicId, CancellationToken ct);

    Task<ProductInfo?> GetProductInfoAsync(int productId, CancellationToken ct);

    /// <summary>Product details for many products in one query, keyed by internal product id — the landing pages
    /// list resolves every page's product without N+1. Unknown ids are absent.</summary>
    Task<Dictionary<int, ProductInfo>> GetProductInfosAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);
}
