namespace CreatorPlatform.Creators.Domain.Creators;

/// <summary>
/// The countries a creator can sell from: creators are paid only through their own connected Stripe (Express) account,
/// with destination charges from our platform account in Croatia. An EEA platform can transfer only to connected accounts
/// in the EEA, the UK, Switzerland, the US and Canada (https://docs.stripe.com/connect/cross-border-payouts), and only
/// countries Stripe supports for accounts count (https://docs.stripe.com/api/country_specs) — Iceland is not one of them.
/// Re-check both pages before launch and whenever a creator's country is missing.
/// </summary>
public static class PayoutCountries
{
    public static readonly IReadOnlySet<string> ConnectCountries = new HashSet<string>(StringComparer.Ordinal)
    {
        // EEA
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU", "IE", "IT", "LI",
        "LT", "LU", "LV", "MT", "NL", "NO", "PL", "PT", "RO", "SE", "SI", "SK",
        // United Kingdom, Switzerland, United States, Canada
        "GB", "CH", "US", "CA",
    };

    public static bool IsSupported(string code) => ConnectCountries.Contains(code);
}
