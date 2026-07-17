namespace CreatorPlatform.Payouts.Application.Options;

public sealed class PayoutsOptions
{
    public const string SectionName = "Payouts";

    public int MinPayoutCents { get; init; } = 5000;

    /// <summary>Recipient for the "creator requested a payout" notification. Null/empty (e.g. unset in an
    /// environment) means the notification is skipped with a warning log — a missing mailbox must never
    /// fail the payout request itself.</summary>
    public string? AdminNotificationEmail { get; init; }
}
