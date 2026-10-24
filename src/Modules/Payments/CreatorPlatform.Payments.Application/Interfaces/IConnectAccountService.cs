using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IConnectAccountService
{
    /// <summary>Creates a new Stripe Connect account for a creator. Returns the Stripe account id.</summary>
    Task<string> CreateAccountAsync(string countryCode, string email, CancellationToken ct);

    /// <summary>Creates a Stripe-hosted onboarding Account Link for the given account.</summary>
    Task<string> CreateOnboardingLinkAsync(string accountId, string returnUrl, string refreshUrl, CancellationToken ct);

    /// <summary>Reads the account's payout schedule live from Stripe. Null when Stripe can't be reached or reports
    /// no schedule — the schedule is informational, so a failure never surfaces as an error.</summary>
    Task<PayoutScheduleDto?> GetPayoutScheduleAsync(string accountId, CancellationToken ct);

    /// <summary>A single-use, short-lived link that signs the account holder into their Stripe Express dashboard.
    /// Stripe failures throw.</summary>
    Task<string> CreateDashboardLoginLinkAsync(string accountId, CancellationToken ct);

    /// <summary>Deletes the connected account once nothing is left on it: its available and pending balances are zero in
    /// every currency. Stripe failures throw (the caller retries).</summary>
    Task<ConnectAccountDeletionOutcome> DeleteIfBalanceIsZeroAsync(string accountId, CancellationToken ct);
}

public enum ConnectAccountDeletionOutcome
{
    Deleted,

    /// <summary>Stripe no longer knows the account (deleted before, or never there).</summary>
    AlreadyGone,

    /// <summary>Money is still on it, or owed by it: kept, and tried again later.</summary>
    BalanceNotZero
}
