using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.LandingPages.Application.Interfaces;

public sealed record CreatorContext(int CreatorId, int MaxLandingPages, int ActiveLandingPageCount)
{
    public PayoutMode PayoutMode { get; init; }
    public bool StripeConnectPayoutsEnabled { get; init; }
    public bool HasPayoutProfile { get; init; }
}

public sealed record ProductInfo(string Name, int PriceCents);

public interface ICreatorContextProvider
{
    Task<CreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<int?> GetProductIdForCreatorAsync(int creatorId, Guid productPublicId, CancellationToken ct);

    Task<ProductInfo?> GetProductInfoAsync(int productId, CancellationToken ct);
}
