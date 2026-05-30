namespace CreatorPlatform.Payments.Application.Dtos;

public sealed class StripeWebhookEventDto
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public CheckoutSessionCompletedData? CheckoutSessionCompleted { get; init; }
    public SubscriptionChangedData? SubscriptionChanged { get; init; }
    public InvoicePaymentFailedData? InvoicePaymentFailed { get; init; }
}

public sealed class CheckoutSessionCompletedData
{
    public required string SessionId { get; init; }
    public required string StripeSubscriptionId { get; init; }
    public required string StripeCustomerId { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
    public DateTimeOffset CurrentPeriodStart { get; init; }
    public DateTimeOffset? CurrentPeriodEnd { get; init; }
}

public sealed class SubscriptionChangedData
{
    public required string StripeSubscriptionId { get; init; }
    public required string StripeCustomerId { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset CurrentPeriodStart { get; init; }
    public DateTimeOffset CurrentPeriodEnd { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed class InvoicePaymentFailedData
{
    public required string StripeSubscriptionId { get; init; }
    public required string StripeCustomerId { get; init; }
}

public static class StripeEventTypes
{
    public const string CheckoutSessionCompleted = "checkout.session.completed";
    public const string CustomerSubscriptionUpdated = "customer.subscription.updated";
    public const string CustomerSubscriptionDeleted = "customer.subscription.deleted";
    public const string InvoicePaymentFailed = "invoice.payment_failed";
}
