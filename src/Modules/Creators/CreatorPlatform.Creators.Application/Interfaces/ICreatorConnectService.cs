using CreatorPlatform.Creators.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

// Split out from ICreatorService — that service is already ~500 lines; Connect onboarding is a
// self-contained concern that doesn't need to grow it further.
public interface ICreatorConnectService
{
    Task<ConnectOnboardingLinkResponseDto> StartConnectOnboardingAsync(int ownerUserId, string ownerEmail, CancellationToken ct);

    /// <summary>404 when the slug isn't the user's workspace. The schedule is read live from Stripe.</summary>
    Task<ConnectPayoutDetailsResponseDto> GetPayoutDetailsAsync(string slug, int ownerUserId, CancellationToken ct);
}
