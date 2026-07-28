namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record PaymentCheckoutSessionDto(string SessionId, string CheckoutUrl);

public interface IPaymentCheckoutSessionService
{
    /// <summary>
    /// <paramref name="applicationFeeAmountCents"/> and <paramref name="destinationAccountId"/> are only
    /// used for StripeConnect creators (destination charge — platform keeps the fee, the rest transfers
    /// to the connected account automatically). Leave both null for BankTransfer creators, which keeps
    /// today's plain platform-charge behavior unchanged. <paramref name="thumbnailUrl"/>, when set, shows
    /// the product's image on the Stripe-hosted checkout page (<c>ProductData.Images</c>) — Stripe
    /// rejects an empty/null entry inside that list, so it's only added when non-blank.
    /// </summary>
    Task<PaymentCheckoutSessionDto> CreateAsync(
        string productName,
        int priceCents,
        string currency,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct,
        int? applicationFeeAmountCents = null,
        string? destinationAccountId = null,
        string? thumbnailUrl = null);
}
