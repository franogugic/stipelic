using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public sealed record CreatorContext(int CreatorId, int MaxLandingPages, int ActiveLandingPageCount)
{
    public CreatorStatus Status { get; init; }
    public PayoutMode PayoutMode { get; init; }
    public bool StripeConnectPayoutsEnabled { get; init; }
    public bool HasPayoutProfile { get; init; }
}

/// <param name="Type">"Digital" | "Service" | "Course".</param>
public sealed record ProductInfo(Guid PublicId, string Name, int PriceCents, string? ThumbnailUrl, string Type);

/// <summary>What a visitor sees of a creator: the brand name (else the workspace name), colour, logo and the
/// currency their products are sold in ("Eur" | "Usd").</summary>
public sealed record PublicCreatorBrand(int CreatorId, string Name, string? PrimaryColor, string? LogoUrl, string Currency);

public interface ICreatorContextProvider
{
    Task<CreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<int?> GetProductIdForCreatorAsync(int creatorId, Guid productPublicId, CancellationToken ct);

    Task<ProductInfo?> GetProductInfoAsync(int productId, CancellationToken ct);

    /// <summary>Product details for many products in one query, keyed by internal product id — the landing pages
    /// list resolves every page's product without N+1. Unknown ids are absent.</summary>
    Task<Dictionary<int, ProductInfo>> GetProductInfosAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);

    Task<PublicCreatorBrand?> GetPublicBrandAsync(int creatorId, CancellationToken ct);

    /// <summary>Null for an unknown or Disabled (deleted) workspace — the same rule as the public pages.</summary>
    Task<PublicCreatorBrand?> GetPublicBrandBySlugAsync(string creatorSlug, CancellationToken ct);
}
