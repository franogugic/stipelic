using CreatorPlatform.Creators.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

// Split out from ICreatorService — that service is already ~500 lines; Connect onboarding is a
// self-contained concern that doesn't need to grow it further.
public interface ICreatorConnectService
{
    Task<ConnectOnboardingLinkResponseDto> StartConnectOnboardingAsync(int ownerUserId, string ownerEmail, CancellationToken ct);

    /// <summary>404 when the slug isn't the user's workspace. The schedule is read live from Stripe.</summary>
    Task<ConnectPayoutDetailsResponseDto> GetPayoutDetailsAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>A login link to the owner's Stripe Express dashboard. 404 without a workspace; 409
    /// <c>connect_dashboard_unavailable</c> unless it is a StripeConnect workspace whose connected account has
    /// submitted its details. Never cached (the link is single-use).</summary>
    Task<ConnectDashboardLinkResponseDto> CreateDashboardLoginLinkAsync(int ownerUserId, CancellationToken ct);
}
