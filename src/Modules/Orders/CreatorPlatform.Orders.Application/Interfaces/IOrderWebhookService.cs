namespace CreatorPlatform.Orders.Application.Interfaces;

/// <param name="CustomerEmail">Stripe's <c>customer_details.email</c>: what the buyer typed (or the prefill).</param>
/// <param name="CustomerName">Stripe's <c>customer_details.name</c>.</param>
public sealed record OrderCheckoutCompletedDto(string SessionId, string? PaymentIntentId, string? CustomerEmail = null, string? CustomerName = null);

public sealed record OrderChargeRefundedDto(string PaymentIntentId, string ChargeId);

public interface IOrderWebhookService
{
    Task HandleCheckoutSessionCompletedAsync(OrderCheckoutCompletedDto data, CancellationToken ct);

    Task HandleChargeRefundedAsync(OrderChargeRefundedDto data, CancellationToken ct);
}
