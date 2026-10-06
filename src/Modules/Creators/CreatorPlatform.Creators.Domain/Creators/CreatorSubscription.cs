namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class CreatorSubscription
{
    private CreatorSubscription()
    {
    }

    private CreatorSubscription(
        Creator creator,
        CreatorPlan plan,
        CreatorSubscriptionStatus status,
        BillingInterval billingInterval,
        SubscriptionProvider provider,
        string? providerSubscriptionId,
        DateTimeOffset? currentPeriodStart,
        DateTimeOffset? currentPeriodEnd,
        DateTimeOffset? trialEndsAt,
        DateTimeOffset createdAt)
    {
        Creator = creator;
        Plan = plan;
        Status = status;
        BillingInterval = billingInterval;
        Provider = provider;
        ProviderSubscriptionId = providerSubscriptionId;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        TrialEndsAt = trialEndsAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static CreatorSubscription CreateFree(
        Creator creator,
        CreatorPlan plan,
        DateTimeOffset createdAt)
    {
        return new CreatorSubscription(
            creator,
            plan,
            CreatorSubscriptionStatus.Active,
            BillingInterval.None,
            SubscriptionProvider.Internal,
            null,
            createdAt,
            null,
            null,
            createdAt);
    }

    public static CreatorSubscription CreatePendingPayment(
        Creator creator,
        CreatorPlan plan,
        BillingInterval billingInterval,
        SubscriptionProvider provider,
        string? providerSubscriptionId,
        DateTimeOffset createdAt)
    {
        return new CreatorSubscription(
            creator,
            plan,
            CreatorSubscriptionStatus.PendingPayment,
            billingInterval,
            provider,
            providerSubscriptionId,
            null,
            null,
            null,
            createdAt);
    }

    public void Activate(
        DateTimeOffset currentPeriodStart,
        DateTimeOffset? currentPeriodEnd,
        DateTimeOffset updatedAt)
    {
        Status = CreatorSubscriptionStatus.Active;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        UpdatedAt = updatedAt;
    }

    /// <summary>Links the paid Stripe subscription and activates. The billing period is set separately
    /// (<see cref="AdvancePeriod"/>) because it comes from a different source than the activation.</summary>
    public void ActivateWithProvider(string providerSubscriptionId, DateTimeOffset updatedAt)
    {
        Status = CreatorSubscriptionStatus.Active;
        Provider = SubscriptionProvider.Stripe;
        ProviderSubscriptionId = providerSubscriptionId;
        UpdatedAt = updatedAt;
    }

    /// <summary>Applies a billing period unless it would move the current one backwards (an earlier end than
    /// the one already stored). Webhooks and the checkout read can deliver periods in any order; the latest
    /// period wins. Returns whether the period was applied.</summary>
    public bool AdvancePeriod(DateTimeOffset currentPeriodStart, DateTimeOffset currentPeriodEnd, DateTimeOffset updatedAt)
    {
        if (CurrentPeriodEnd is { } storedEnd && currentPeriodEnd < storedEnd)
            return false;

        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        UpdatedAt = updatedAt;
        return true;
    }

    /// <summary>True when a subscription event happened strictly before the last one already applied. Equal
    /// timestamps are not stale: Stripe's event time has one-second resolution, and created/updated for the
    /// same subscription regularly share a second (the period guard still keeps the newer period).</summary>
    public bool IsStaleProviderEvent(DateTimeOffset eventOccurredAt)
        => ProviderEventAt is { } lastEventAt && eventOccurredAt < lastEventAt;

    /// <summary>Remembers the time of the latest applied subscription event (never moves backwards).</summary>
    public void RecordProviderEvent(DateTimeOffset eventOccurredAt, DateTimeOffset updatedAt)
    {
        if (ProviderEventAt is { } lastEventAt && eventOccurredAt <= lastEventAt)
            return;

        ProviderEventAt = eventOccurredAt;
        UpdatedAt = updatedAt;
    }

    public void UpdatePlan(CreatorPlan newPlan, DateTimeOffset updatedAt)
    {
        Plan = newPlan;
        UpdatedAt = updatedAt;
    }

    public void MarkPastDue(DateTimeOffset updatedAt)
    {
        Status = CreatorSubscriptionStatus.PastDue;
        UpdatedAt = updatedAt;
    }

    public void ScheduleCancel(DateTimeOffset updatedAt)
    {
        CancelAtPeriodEnd = true;
        UpdatedAt = updatedAt;
    }

    public void UndoScheduledCancel(DateTimeOffset updatedAt)
    {
        CancelAtPeriodEnd = false;
        UpdatedAt = updatedAt;
    }

    /// <summary>Records the Stripe Checkout session opened for this pending subscription, replacing any
    /// earlier one (a re-checkout creates a new session), so it can be expired if the creator abandons the
    /// payment.</summary>
    public void AttachCheckoutSession(string checkoutSessionId, DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(checkoutSessionId))
            throw new ArgumentException("Checkout session id is required.", nameof(checkoutSessionId));

        CheckoutSessionId = checkoutSessionId;
        UpdatedAt = updatedAt;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        Status = CreatorSubscriptionStatus.Cancelled;
        CancelAtPeriodEnd = false;
        CancelledAt = cancelledAt;
        UpdatedAt = cancelledAt;
    }

    public int Id { get; private set; }

    public int CreatorId { get; private set; }

    public Creator Creator { get; private set; } = null!;

    public int PlanId { get; private set; }

    public CreatorPlan Plan { get; private set; } = null!;

    public CreatorSubscriptionStatus Status { get; private set; }

    public BillingInterval BillingInterval { get; private set; }

    public SubscriptionProvider Provider { get; private set; }

    public string? ProviderSubscriptionId { get; private set; }

    public DateTimeOffset? CurrentPeriodStart { get; private set; }

    public DateTimeOffset? CurrentPeriodEnd { get; private set; }

    public DateTimeOffset? TrialEndsAt { get; private set; }

    public bool CancelAtPeriodEnd { get; private set; }

    /// <summary>The latest Stripe Checkout session opened for this subscription while it was pending; null
    /// for free subscriptions and for pending ones created before this was recorded.</summary>
    public string? CheckoutSessionId { get; private set; }

    /// <summary>Stripe's <c>Created</c> time of the latest applied customer.subscription.* event — rejects
    /// stale, out-of-order deliveries (same pattern as <c>Creator.StripeConnectStatusEventAt</c>). Null until the
    /// first one: the next event is accepted.</summary>
    public DateTimeOffset? ProviderEventAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
