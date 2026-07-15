namespace CreatorPlatform.Payments.Application.Dtos;

public sealed class StripeWebhookEventDto
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public CheckoutSessionCompletedData? CheckoutSessionCompleted { get; init; }
    public SubscriptionChangedData? SubscriptionChanged { get; init; }
    public InvoicePaymentFailedData? InvoicePaymentFailed { get; init; }
    public ChargeRefundedData? ChargeRefunded { get; init; }
    public AccountUpdatedData? AccountUpdated { get; init; }
}

public sealed class AccountUpdatedData
{
    public required string AccountId { get; init; }
    public bool DetailsSubmitted { get; init; }
    public bool ChargesEnabled { get; init; }
    public bool PayoutsEnabled { get; init; }

    /// <summary>The Stripe event's own timestamp (not when we received it) — Stripe does not guarantee
    /// delivery order, so this is used to reject a stale/out-of-order replay of an older account state.</summary>
    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class CheckoutSessionCompletedData
{
    public required string SessionId { get; init; }
    public required string StripeSubscriptionId { get; init; }
    public required string StripeCustomerId { get; init; }
    public string? StripePaymentIntentId { get; init; }
    public required IReadOnlyDictionary<string, string> Metadata { get; init; }
    public DateTimeOffset CurrentPeriodStart { get; init; }
    public DateTimeOffset? CurrentPeriodEnd { get; init; }
}

public sealed class SubscriptionChangedData
{
    public required string StripeSubscriptionId { get; init; }
    public required string StripeCustomerId { get; init; }
    public required string Status { get; init; }
    public bool CancelAtPeriodEnd { get; init; }
    public string? StripePriceId { get; init; }
    public DateTimeOffset CurrentPeriodStart { get; init; }
    public DateTimeOffset CurrentPeriodEnd { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed class InvoicePaymentFailedData
{
    public required string StripeSubscriptionId { get; init; }
    public required string StripeCustomerId { get; init; }
}

public sealed class ChargeRefundedData
{
    public required string PaymentIntentId { get; init; }
    public required string ChargeId { get; init; }
}

public static class StripeEventTypes
{
    public const string CheckoutSessionCompleted = "checkout.session.completed";
    public const string CustomerSubscriptionUpdated = "customer.subscription.updated";
    public const string CustomerSubscriptionDeleted = "customer.subscription.deleted";
    public const string InvoicePaymentFailed = "invoice.payment_failed";
    public const string ChargeRefunded = "charge.refunded";
    public const string AccountUpdated = "account.updated";
}
