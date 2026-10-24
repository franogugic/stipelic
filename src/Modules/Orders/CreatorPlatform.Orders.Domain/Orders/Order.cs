using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Orders.Domain.Orders;

public sealed class Order
{
    private Order()
    {
    }

    private Order(
        Guid publicId,
        int creatorId,
        int productId,
        int? landingPageId,
        string? email,
        string? name,
        int amountCents,
        Currency currency,
        OrderStatus status,
        int platformFeeBasisPoints,
        int platformFeeCents,
        string payoutMode,
        string? stripeCheckoutSessionId,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        ProductId = productId;
        LandingPageId = landingPageId;
        Email = email;
        Name = name;
        AmountCents = amountCents;
        Currency = currency;
        Status = status;
        PlatformFeeBasisPoints = platformFeeBasisPoints;
        PlatformFeeCents = platformFeeCents;
        PayoutMode = payoutMode;
        StripeCheckoutSessionId = stripeCheckoutSessionId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Order Create(
        int creatorId,
        int productId,
        int? landingPageId,
        string? email,
        string? name,
        int amountCents,
        Currency currency,
        int platformFeeBasisPoints,
        int platformFeeCents,
        string payoutMode,
        string? stripeCheckoutSessionId,
        DateTimeOffset createdAt)
    {
        return new Order(
            Guid.NewGuid(),
            creatorId,
            productId,
            landingPageId,
            email,
            name,
            amountCents,
            currency,
            OrderStatus.Pending,
            platformFeeBasisPoints,
            platformFeeCents,
            payoutMode,
            stripeCheckoutSessionId,
            createdAt);
    }

    /// <summary>Links the Stripe Checkout session created for this (already saved) Pending order. Repeating it with the
    /// same session is a no-op; a second, different session for one order is a bug and throws.</summary>
    public void AttachCheckoutSession(string stripeCheckoutSessionId, DateTimeOffset attachedAt)
    {
        if (string.IsNullOrWhiteSpace(stripeCheckoutSessionId))
            throw new ArgumentException("A Checkout session id is required.", nameof(stripeCheckoutSessionId));

        if (StripeCheckoutSessionId == stripeCheckoutSessionId)
            return;

        if (StripeCheckoutSessionId is not null)
            throw new InvalidOperationException(
                $"Order {PublicId} already has Checkout session {StripeCheckoutSessionId}; refusing {stripeCheckoutSessionId}.");

        StripeCheckoutSessionId = stripeCheckoutSessionId;
        UpdatedAt = attachedAt;
    }

    /// <summary>Stripe Checkout collects the buyer's email when the page didn't ask for it, so it arrives here.
    /// An email given at checkout (passed to Stripe as a locked prefill) is kept. A paid order always has one.</summary>
    public void MarkPaid(string stripePaymentIntentId, DateTimeOffset paidAt, string? buyerEmail, string? buyerName)
    {
        var email = Email ?? (string.IsNullOrWhiteSpace(buyerEmail) ? null : buyerEmail.Trim());
        if (email is null)
            throw new InvalidOperationException($"Order {PublicId} cannot be paid without a buyer email.");

        Email = email;
        if (Name is null && !string.IsNullOrWhiteSpace(buyerName))
        {
            var name = buyerName.Trim();
            Name = name.Length > NameMaxLength ? name[..NameMaxLength] : name;
        }

        Status = OrderStatus.Paid;
        StripePaymentIntentId = stripePaymentIntentId;
        PaidAt = paidAt;
        UpdatedAt = paidAt;
    }

    /// <summary>A Pending order that can no longer be paid: its session expired, or creating the session failed.</summary>
    public void MarkFailed(DateTimeOffset updatedAt)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Order {PublicId} is {Status}; only a Pending order can fail.");

        Status = OrderStatus.Failed;
        UpdatedAt = updatedAt;
    }

    /// <summary>Applies Stripe's cumulative refunded amount, converted to the order's currency. Below
    /// <see cref="AmountCents"/> the order stays Paid ("partly refunded") and the buyer keeps access; at the full amount
    /// it becomes Refunded. Returns false, changing nothing, when the amount is not above what is already applied
    /// (a replayed or out-of-order event) or the order is not Paid.</summary>
    public bool ApplyRefund(int refundedToCents, DateTimeOffset refundedAt)
    {
        if (Status != OrderStatus.Paid || refundedToCents <= RefundedCents)
            return false;

        if (refundedToCents >= AmountCents)
        {
            RefundedCents = AmountCents;
            Status = OrderStatus.Refunded;
        }
        else
        {
            RefundedCents = refundedToCents;
        }

        UpdatedAt = refundedAt;
        return true;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public int ProductId { get; private set; }

    public int? LandingPageId { get; private set; }

    public const int NameMaxLength = 50;

    /// <summary>Null only while Pending, when the buyer hasn't typed it into Stripe Checkout yet.</summary>
    public string? Email { get; private set; }

    public string? Name { get; private set; }

    public int AmountCents { get; private set; }

    public Currency Currency { get; private set; }

    public OrderStatus Status { get; private set; }

    /// <summary>Snapshot of the creator plan's fee at the time of purchase — never recomputed later.</summary>
    public int PlatformFeeBasisPoints { get; private set; }

    /// <summary>Snapshot of <see cref="PlatformFeeBasisPoints"/> applied to <see cref="AmountCents"/> — see PlatformFee.Calculate.</summary>
    public int PlatformFeeCents { get; private set; }

    /// <summary>Snapshot of the creator's payout mode at purchase time ("StripeConnect" / "BankTransfer").</summary>
    public string PayoutMode { get; private set; } = string.Empty;

    /// <summary>Null only between saving the Pending order and creating its Checkout session — and for good when
    /// creating the session failed (the order is then Failed).</summary>
    public string? StripeCheckoutSessionId { get; private set; }

    public string? StripePaymentIntentId { get; private set; }

    /// <summary>How much of <see cref="AmountCents"/> has been refunded, in the order's currency. Equal to it once
    /// Refunded; between 0 and it on a Paid order that was partly refunded.</summary>
    public int RefundedCents { get; private set; }

    public bool IsPartlyRefunded => Status == OrderStatus.Paid && RefundedCents > 0;

    /// <summary>What the sale still keeps: <see cref="AmountCents"/> minus <see cref="RefundedCents"/>. A stored column
    /// computed by the database — the figure every revenue total sums; stale in memory until the order is saved.</summary>
    public int KeptAmountCents { get; private set; }

    /// <summary>The platform fee the sale still keeps: the fee minus its share of the refunded part, rounded as the
    /// ledger's fee refund is. Computed by the database, like <see cref="KeptAmountCents"/>.</summary>
    public int KeptPlatformFeeCents { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
