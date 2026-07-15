namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreatorResponseDto
{
    public Guid PublicId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string DefaultCurrency { get; init; } = string.Empty;

    public string PlanCode { get; init; } = string.Empty;

    public bool CancelAtPeriodEnd { get; init; }

    public string CountryCode { get; init; } = string.Empty;

    public string PayoutMode { get; init; } = string.Empty;

    public bool StripeConnectDetailsSubmitted { get; init; }

    public bool StripeConnectPayoutsEnabled { get; init; }

    public bool HasPayoutProfile { get; init; }

    /// <summary>Connect creators: ready once Stripe reports payouts enabled. BankTransfer creators: ready once an IBAN profile exists.</summary>
    public bool PayoutReady { get; init; }
}
