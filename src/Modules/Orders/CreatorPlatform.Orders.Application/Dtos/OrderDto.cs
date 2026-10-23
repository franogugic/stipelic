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
    DateTimeOffset? LastDownloadedAt = null);
