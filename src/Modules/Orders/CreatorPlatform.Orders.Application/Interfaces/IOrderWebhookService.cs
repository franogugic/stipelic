namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record OrderCheckoutCompletedDto(string SessionId, string? PaymentIntentId);

public sealed record OrderChargeRefundedDto(string PaymentIntentId, string ChargeId);

public interface IOrderWebhookService
{
    Task HandleCheckoutSessionCompletedAsync(OrderCheckoutCompletedDto data, CancellationToken ct);

    Task HandleChargeRefundedAsync(OrderChargeRefundedDto data, CancellationToken ct);
}
