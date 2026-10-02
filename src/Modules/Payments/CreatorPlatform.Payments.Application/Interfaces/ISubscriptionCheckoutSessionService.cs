using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface ISubscriptionCheckoutSessionService
{
    Task<SubscriptionCheckoutSessionDto> CreateAsync(
        string stripePriceId,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct);

    /// <summary>Makes the session unpayable. Throws when the provider can't be reached or answers
    /// unexpectedly — the caller must then assume the session may still be payable.</summary>
    Task<CheckoutSessionExpireOutcome> ExpireAsync(string checkoutSessionId, CancellationToken ct);
}
