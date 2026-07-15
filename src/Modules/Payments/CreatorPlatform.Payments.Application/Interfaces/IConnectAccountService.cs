namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IConnectAccountService
{
    /// <summary>Creates a new Stripe Connect account for a creator. Returns the Stripe account id.</summary>
    Task<string> CreateAccountAsync(string countryCode, string email, CancellationToken ct);

    /// <summary>Creates a Stripe-hosted onboarding Account Link for the given account.</summary>
    Task<string> CreateOnboardingLinkAsync(string accountId, string returnUrl, string refreshUrl, CancellationToken ct);
}
