namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record PaymentCheckoutSessionDto(string SessionId, string CheckoutUrl);

public interface IPaymentCheckoutSessionService
{
    Task<PaymentCheckoutSessionDto> CreateAsync(
        string productName,
        int priceCents,
        string currency,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct);
}
