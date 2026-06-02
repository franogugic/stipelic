using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorWebhookService
{
    Task HandleCheckoutSessionCompletedAsync(CheckoutSessionCompletedData data, CancellationToken ct);

    Task HandleSubscriptionUpdatedAsync(SubscriptionChangedData data, CancellationToken ct);

    Task HandleSubscriptionDeletedAsync(SubscriptionChangedData data, CancellationToken ct);

    Task HandleInvoicePaymentFailedAsync(InvoicePaymentFailedData data, CancellationToken ct);
}
