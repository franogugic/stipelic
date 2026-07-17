namespace CreatorPlatform.Payouts.Application.Dtos;

public sealed record RequestPayoutRequestDto
{
    /// <summary>Null/omitted means "request the full available balance" — computed under the advisory
    /// lock at request time, not on the client.</summary>
    public int? AmountCents { get; init; }
}
