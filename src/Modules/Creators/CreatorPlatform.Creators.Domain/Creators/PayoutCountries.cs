namespace CreatorPlatform.Creators.Domain.Creators;

/// <summary>
/// Registry of ISO 3166-1 alpha-2 country codes and which payout rail they resolve to.
/// <see cref="ConnectCountries"/> mirrors Stripe's supported-countries list for Connect accounts at the
/// time this was written — Stripe periodically expands this list, so it should be re-checked against
/// https://stripe.com/global when onboarding creators from a country not yet listed here.
/// </summary>
public static class PayoutCountries
{
    public static readonly IReadOnlySet<string> ConnectCountries = new HashSet<string>(StringComparer.Ordinal)
    {
        "AU", "AT", "BE", "BG", "CA", "HR", "CY", "CZ", "DK", "EE",
        "FI", "FR", "DE", "GI", "GR", "HK", "HU", "IN", "ID", "IE",
        "IT", "JP", "LV", "LI", "LT", "LU", "MY", "MT", "MX", "NL",
        "NZ", "NO", "PH", "PL", "PT", "RO", "SG", "SK", "SI", "ES",
        "SE", "CH", "TH", "AE", "GB", "US",
    };

    public static readonly IReadOnlySet<string> BankTransferCountries = new HashSet<string>(StringComparer.Ordinal)
    {
        "RS", "BA", "ME", "MK", "AL",
    };

    public static bool IsSupported(string code)
    {
        return ConnectCountries.Contains(code) || BankTransferCountries.Contains(code);
    }

    /// <summary>Connect takes precedence in the (currently theoretical) case a country lands in both sets.</summary>
    public static PayoutMode ResolveMode(string code)
    {
        if (ConnectCountries.Contains(code))
            return PayoutMode.StripeConnect;

        if (BankTransferCountries.Contains(code))
            return PayoutMode.BankTransfer;

        throw new ArgumentOutOfRangeException(nameof(code), code, "Country is not supported for payouts.");
    }
}
