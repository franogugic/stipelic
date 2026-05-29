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

    public void MarkPastDue(DateTimeOffset updatedAt)
    {
        Status = CreatorSubscriptionStatus.PastDue;
        UpdatedAt = updatedAt;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        Status = CreatorSubscriptionStatus.Cancelled;
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

    public DateTimeOffset? CancelledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
