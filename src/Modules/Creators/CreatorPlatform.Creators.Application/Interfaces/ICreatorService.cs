using CreatorPlatform.Creators.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorService
{
    Task<CreatorResponseDto?> GetCurrentForOwnerAsync(int ownerUserId, CancellationToken ct);

    Task<CreatorSettingsResponseDto> GetSettingsAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<CreatorSettingsResponseDto> UpdateSettingsAsync(
        string slug,
        int ownerUserId,
        UpdateCreatorSettingsRequestDto request,
        CancellationToken ct);

    Task<CreateCreatorResponseDto> CreateAsync(int ownerUserId, CreateCreatorRequestDto request, CancellationToken ct);

    Task<StartCreatorSubscriptionCheckoutResponseDto> StartSubscriptionCheckoutAsync(
        int ownerUserId,
        CancellationToken ct);

    Task CancelSubscriptionAsync(int ownerUserId, CancellationToken ct);

    Task<string> GetBillingPortalUrlAsync(int ownerUserId, CancellationToken ct);

    Task DeleteCurrentAsync(int ownerUserId, CancellationToken ct);

    /// <summary>Static registry data — no I/O, so synchronous (same style as other section-template style lookups).</summary>
    List<PayoutCountryDto> GetPayoutCountries();
}
