namespace CreatorPlatform.Orders.Application.Dtos;

public sealed record OrderDto(
    Guid PublicId,
    string? Email,
    string? Name,
    string ProductName,
    int AmountCents,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    int PlatformFeeCents,
    int NetAmountCents,
    string? LandingPageTitle,
    // The buyer's personal link (F8): how often they downloaded the file. 0 before payment.
    int DownloadCount = 0,
    DateTimeOffset? LastDownloadedAt = null,
    // R6.5: the date the period rule counts the order on — PaidAt (else CreatedAt) when Paid, the refund time when
    // Refunded, CreatedAt when Pending or Failed.
    DateTimeOffset CountedAt = default,
    // R6.5: the latest refund time of an order with a refund, full or partial (its UpdatedAt), else null.
    DateTimeOffset? RefundedAt = null,
    // B3: how much was refunded (order currency). The whole amount when Refunded; above 0 on a Paid order means
    // "partly refunded" — the buyer keeps access.
    int RefundedCents = 0);
