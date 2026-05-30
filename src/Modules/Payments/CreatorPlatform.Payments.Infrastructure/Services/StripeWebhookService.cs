using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace CreatorPlatform.Payments.Infrastructure.Services;

public sealed class StripeWebhookService : IStripeWebhookService
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripeWebhookService> _logger;

    public StripeWebhookService(
        IOptions<StripeOptions> options,
        ILogger<StripeWebhookService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public StripeWebhookEventDto ParseAndVerify(string payload, string stripeSignature)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            _logger.LogError("Stripe webhook secret is not configured.");
            throw new BadRequestException("Stripe webhook secret is not configured.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                stripeSignature,
                _options.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed.");
            throw new BadRequestException("Stripe webhook signature verification failed.");
        }

        _logger.LogInformation(
            "Stripe webhook event received. EventId: {EventId}, EventType: {EventType}",
            stripeEvent.Id,
            stripeEvent.Type);

        return stripeEvent.Type switch
        {
            StripeEventTypes.CheckoutSessionCompleted => MapCheckoutSessionCompleted(stripeEvent),
            StripeEventTypes.CustomerSubscriptionUpdated => MapSubscriptionChanged(stripeEvent),
            StripeEventTypes.CustomerSubscriptionDeleted => MapSubscriptionChanged(stripeEvent),
            StripeEventTypes.InvoicePaymentFailed => MapInvoicePaymentFailed(stripeEvent),
            _ => new StripeWebhookEventDto
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type
            }
        };
    }

    private static StripeWebhookEventDto MapCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session
            ?? throw new InvalidOperationException(
                $"Expected Session object in checkout.session.completed event. EventId: {stripeEvent.Id}");

        return new StripeWebhookEventDto
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            CheckoutSessionCompleted = new CheckoutSessionCompletedData
            {
                SessionId = session.Id,
                StripeSubscriptionId = session.SubscriptionId ?? string.Empty,
                StripeCustomerId = session.CustomerId ?? string.Empty,
                Metadata = session.Metadata ?? new Dictionary<string, string>(),
                CurrentPeriodStart = DateTimeOffset.UtcNow,
                CurrentPeriodEnd = null
            }
        };
    }

    private StripeWebhookEventDto MapSubscriptionChanged(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription
            ?? throw new InvalidOperationException(
                $"Expected Subscription object in {stripeEvent.Type} event. EventId: {stripeEvent.Id}");

        // In Stripe.net v51, period dates are on individual subscription items
        var firstItem = subscription.Items?.Data?.FirstOrDefault();
        if (firstItem is null)
        {
            _logger.LogWarning(
                "Subscription has no items, cannot extract period dates. EventId: {EventId}, StripeSubscriptionId: {StripeSubscriptionId}",
                stripeEvent.Id, subscription.Id);
        }

        var periodStart = firstItem?.CurrentPeriodStart ?? DateTime.UtcNow;
        var periodEnd = firstItem?.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);

        return new StripeWebhookEventDto
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            SubscriptionChanged = new SubscriptionChangedData
            {
                StripeSubscriptionId = subscription.Id,
                StripeCustomerId = subscription.CustomerId,
                Status = subscription.Status,
                CurrentPeriodStart = new DateTimeOffset(periodStart, TimeSpan.Zero),
                CurrentPeriodEnd = new DateTimeOffset(periodEnd, TimeSpan.Zero),
                Metadata = subscription.Metadata ?? new Dictionary<string, string>()
            }
        };
    }

    private static StripeWebhookEventDto MapInvoicePaymentFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice
            ?? throw new InvalidOperationException(
                $"Expected Invoice object in invoice.payment_failed event. EventId: {stripeEvent.Id}");

        // In Stripe.net v51, subscription ID is nested under invoice.Parent.SubscriptionDetails
        var stripeSubscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId ?? string.Empty;

        return new StripeWebhookEventDto
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            InvoicePaymentFailed = new InvoicePaymentFailedData
            {
                StripeSubscriptionId = stripeSubscriptionId,
                StripeCustomerId = invoice.CustomerId ?? string.Empty
            }
        };
    }
}
