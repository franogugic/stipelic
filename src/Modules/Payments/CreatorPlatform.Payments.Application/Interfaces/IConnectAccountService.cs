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
}
