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
        string email,
        string? name,
        int amountCents,
        string currency,
        OrderStatus status,
        string stripeCheckoutSessionId,
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
        StripeCheckoutSessionId = stripeCheckoutSessionId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Order Create(
        int creatorId,
        int productId,
        int? landingPageId,
        string email,
        string? name,
        int amountCents,
        string currency,
        string stripeCheckoutSessionId,
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
            stripeCheckoutSessionId,
            createdAt);
    }

    public void MarkPaid(string stripePaymentIntentId, DateTimeOffset paidAt)
    {
        Status = OrderStatus.Paid;
        StripePaymentIntentId = stripePaymentIntentId;
        PaidAt = paidAt;
        UpdatedAt = paidAt;
    }

    public void MarkFailed(DateTimeOffset updatedAt)
    {
        Status = OrderStatus.Failed;
        UpdatedAt = updatedAt;
    }

    public void MarkRefunded(DateTimeOffset updatedAt)
    {
        Status = OrderStatus.Refunded;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public int ProductId { get; private set; }

    public int? LandingPageId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string? Name { get; private set; }

    public int AmountCents { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public string StripeCheckoutSessionId { get; private set; } = string.Empty;

    public string? StripePaymentIntentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
