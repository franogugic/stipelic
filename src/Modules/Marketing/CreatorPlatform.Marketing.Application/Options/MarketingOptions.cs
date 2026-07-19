namespace CreatorPlatform.Marketing.Application.Options;

public sealed class MarketingOptions
{
    public const string SectionName = "Marketing";

    /// <summary>HMAC-SHA256 signing secret for unsubscribe tokens (see IUnsubscribeTokenService). Must be
    /// a long random string, kept out of source control (appsettings.Development.json is gitignored) and
    /// rotated only with awareness that rotating it invalidates every unsubscribe link already sent.</summary>
    public string UnsubscribeTokenSecret { get; init; } = string.Empty;
}
