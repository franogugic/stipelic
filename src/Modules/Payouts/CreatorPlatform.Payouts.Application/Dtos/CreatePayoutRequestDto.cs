namespace CreatorPlatform.Payouts.Application.Dtos;

public sealed record CreatePayoutRequestDto
{
    public Guid CreatorPublicId { get; init; }
    public int AmountCents { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string? Note { get; init; }
}

public sealed record MarkPayoutPaidRequestDto
{
    public string BankReference { get; init; } = string.Empty;
}

public sealed record MarkPayoutFailedRequestDto
{
    public string? Note { get; init; }
}
