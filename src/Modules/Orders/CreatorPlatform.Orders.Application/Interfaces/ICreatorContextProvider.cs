using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record LandingPageProductInfo(
    int CreatorId,
    int ProductId,
    int LandingPageId,
    string ProductName,
    string? ThumbnailUrl,
    int PriceCents,
    Currency Currency,
    CreatorStatus CreatorStatus,
    PayoutMode PayoutMode,
    string? StripeConnectAccountId,
    bool StripeConnectPayoutsEnabled,
    bool HasPayoutProfile,
    // Null when the creator has no active subscription (checkout is rejected before this matters).
    int? PlatformFeeBasisPoints);

/// <param name="ProductTypeLabel">"Digital download", "Service" or "Online course".</param>
/// <param name="CreatorName">The brand name when the creator saved one, else the workspace name.</param>
public sealed record OrderEmailContext(
    string ProductName,
    string ProductTypeLabel,
    string? ProductThumbnailUrl,
    string CreatorName,
    string? BrandColor,
    string? LogoUrl,
    string? SupportEmail);

public interface ICreatorContextProvider
{
    Task<LandingPageProductInfo?> GetProductInfoByLandingPageSlugAsync(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct);

    /// <summary>What the buyer's order email shows about the product and the creator's brand, in one query. Null when
    /// the product no longer exists.</summary>
    Task<OrderEmailContext?> GetOrderEmailContextAsync(int productId, CancellationToken ct);

}
