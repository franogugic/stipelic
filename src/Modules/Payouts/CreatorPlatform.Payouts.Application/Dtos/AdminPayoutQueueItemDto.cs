namespace CreatorPlatform.Payouts.Application.Dtos;

/// <summary>Payout row for the platform admin queue — includes the full IBAN (not masked) because the
/// admin needs it to execute the bank transfer. Only ever returned behind the platform_admin role.</summary>
public sealed record AdminPayoutQueueItemDto(
    Guid PublicId,
    Guid CreatorPublicId,
    string CreatorName,
    string CreatorSlug,
    int AmountCents,
    string Currency,
    string Status,
    string? BankReference,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    string AccountHolderName,
    string Iban,
    string BankCountryCode);
