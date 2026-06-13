namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record OrderCheckoutCompletedDto(string SessionId, string? PaymentIntentId);

public interface IOrderWebhookService
{
    Task HandleCheckoutSessionCompletedAsync(OrderCheckoutCompletedDto data, CancellationToken ct);
}
