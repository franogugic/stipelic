namespace CreatorPlatform.Payouts.Application.Dtos;

public sealed record PayoutDto(
    Guid PublicId,
    int AmountCents,
    string Currency,
    string Status,
    string? BankReference,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);
