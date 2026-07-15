using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class Creator
{
    private Creator()
    {
    }

    private Creator(
        Guid publicId,
        int ownerUserId,
        string name,
        string slug,
        Currency defaultCurrency,
        CreatorStatus status,
        string countryCode,
        PayoutMode payoutMode,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        OwnerUserId = ownerUserId;
        Name = name;
        Slug = slug;
        DefaultCurrency = defaultCurrency;
        Status = status;
        CountryCode = countryCode;
        PayoutMode = payoutMode;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Creator Create(
        int ownerUserId,
        string name,
        string slug,
        Currency defaultCurrency,
        CreatorStatus status,
        string countryCode,
        PayoutMode payoutMode,
        DateTimeOffset createdAt)
    {
        return new Creator(
            Guid.NewGuid(),
            ownerUserId,
            name,
            slug,
            defaultCurrency,
            status,
            countryCode,
            payoutMode,
            createdAt);
    }

    public void Rename(string name, string slug, DateTimeOffset updatedAt)
    {
        Name = name;
        Slug = slug;
        UpdatedAt = updatedAt;
    }

    public void ChangeDefaultCurrency(Currency defaultCurrency, DateTimeOffset updatedAt)
    {
        DefaultCurrency = defaultCurrency;
        UpdatedAt = updatedAt;
    }

    public void Suspend(DateTimeOffset updatedAt)
    {
        Status = CreatorStatus.Suspended;
        UpdatedAt = updatedAt;
    }

    public void Disable(DateTimeOffset updatedAt)
    {
        Status = CreatorStatus.Disabled;
        UpdatedAt = updatedAt;
    }

    public void Activate(DateTimeOffset updatedAt)
    {
        Status = CreatorStatus.Active;
        UpdatedAt = updatedAt;
    }

    public void SetStripeCustomerId(string stripeCustomerId, DateTimeOffset updatedAt)
    {
        StripeCustomerId = stripeCustomerId;
        UpdatedAt = updatedAt;
    }

    public void SetStripeConnectAccountId(string stripeConnectAccountId, DateTimeOffset updatedAt)
    {
        StripeConnectAccountId = stripeConnectAccountId;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Applies Stripe's reported Connect account state. <paramref name="eventOccurredAt"/> is the Stripe
    /// event's own timestamp — Stripe does not guarantee delivery order, so an event older than the last
    /// one we already applied is a no-op (protects against a delayed/replayed event overwriting newer state).
    /// </summary>
    public void UpdateStripeConnectStatus(
        bool detailsSubmitted,
        bool chargesEnabled,
        bool payoutsEnabled,
        DateTimeOffset eventOccurredAt,
        DateTimeOffset updatedAt)
    {
        if (StripeConnectStatusEventAt is { } lastEventAt && eventOccurredAt <= lastEventAt)
            return;

        StripeConnectDetailsSubmitted = detailsSubmitted;
        StripeConnectChargesEnabled = chargesEnabled;
        StripeConnectPayoutsEnabled = payoutsEnabled;
        StripeConnectStatusEventAt = eventOccurredAt;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public CreatorStatus Status { get; private set; }

    public Currency DefaultCurrency { get; private set; }

    public string? StripeCustomerId { get; private set; }

    public string CountryCode { get; private set; } = string.Empty;

    public PayoutMode PayoutMode { get; private set; }

    public string? StripeConnectAccountId { get; private set; }

    public bool StripeConnectDetailsSubmitted { get; private set; }

    public bool StripeConnectChargesEnabled { get; private set; }

    public bool StripeConnectPayoutsEnabled { get; private set; }

    public DateTimeOffset? StripeConnectStatusEventAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
