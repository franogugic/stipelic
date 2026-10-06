namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreatorResponseDto
{
    public Guid PublicId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string DefaultCurrency { get; init; } = string.Empty;

    public string PlanCode { get; init; } = string.Empty;

    /// <summary>Display name of the current plan (e.g. "Pro"); empty when the creator has no subscription.</summary>
    public string PlanName { get; init; } = string.Empty;

    /// <summary>Status of the current subscription: "PendingPayment", "Active" or "PastDue"; null when the creator has
    /// none. (A Cancelled subscription is never the current one, so "Cancelled" does not occur here.)</summary>
    public string? SubscriptionStatus { get; init; }

    public bool CancelAtPeriodEnd { get; init; }

    /// <summary>End of the current billing period — the renewal or cancellation date; null when unknown (e.g. the free plan).</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; init; }

    public string CountryCode { get; init; } = string.Empty;

    public string PayoutMode { get; init; } = string.Empty;

    public bool StripeConnectDetailsSubmitted { get; init; }

    public bool StripeConnectPayoutsEnabled { get; init; }

    public bool HasPayoutProfile { get; init; }

    /// <summary>Connect creators: ready once Stripe reports payouts enabled. BankTransfer creators: ready once an IBAN profile exists.</summary>
    public bool PayoutReady { get; init; }
}
