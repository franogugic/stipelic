using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record LandingPageProductInfo(
    int CreatorId,
    int ProductId,
    int LandingPageId,
    string ProductName,
    int PriceCents,
    Currency Currency,
    CreatorStatus CreatorStatus,
    PayoutMode PayoutMode,
    string? StripeConnectAccountId,
    bool StripeConnectPayoutsEnabled,
    bool HasPayoutProfile,
    // Null when the creator has no active subscription (checkout is rejected before this matters).
    int? PlatformFeeBasisPoints);

public interface ICreatorContextProvider
{
    Task<LandingPageProductInfo?> GetProductInfoByLandingPageSlugAsync(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct);

    Task<string?> GetProductNameAsync(int productId, CancellationToken ct);

    Task<string?> GetCreatorSlugByIdAsync(int creatorId, CancellationToken ct);
}
