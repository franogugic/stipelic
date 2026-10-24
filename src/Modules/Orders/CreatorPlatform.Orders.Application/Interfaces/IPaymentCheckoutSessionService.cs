namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record PaymentCheckoutSessionDto(string SessionId, string CheckoutUrl);

public interface IPaymentCheckoutSessionService
{
    /// <summary>
    /// A destination charge: the buyer pays on our platform account, Stripe transfers the sale to the creator's connected
    /// account <paramref name="destinationAccountId"/> and keeps <paramref name="applicationFeeAmountCents"/> for us.
    /// <paramref name="thumbnailUrl"/>, when set, shows the product's image on the Stripe-hosted checkout page
    /// (<c>ProductData.Images</c>) — Stripe rejects an empty/null entry inside that list, so it's only added when non-blank.
    /// </summary>
    Task<PaymentCheckoutSessionDto> CreateAsync(
        string productName,
        int priceCents,
        string currency,
        string? customerEmail,
        string successUrl,
        string cancelUrl,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        int applicationFeeAmountCents,
        string destinationAccountId,
        CancellationToken ct,
        string? thumbnailUrl = null);
}
