namespace CreatorPlatform.Orders.Application.Dtos;

public sealed record OrderDto(
    Guid PublicId,
    string Email,
    string? Name,
    string ProductName,
    int AmountCents,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);
