namespace CreatorPlatform.Payouts.Application.Options;

public sealed class PayoutsOptions
{
    public const string SectionName = "Payouts";

    public int MinPayoutCents { get; init; } = 5000;
}
