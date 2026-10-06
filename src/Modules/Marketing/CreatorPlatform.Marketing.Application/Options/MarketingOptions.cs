namespace CreatorPlatform.Marketing.Application.Options;

public sealed class MarketingOptions
{
    public const string SectionName = "Marketing";

    /// <summary>HMAC-SHA256 signing secret for unsubscribe tokens (see IUnsubscribeTokenService). Must be
    /// a long random string, kept out of source control (appsettings.Development.json is gitignored) and
    /// rotated only with awareness that rotating it invalidates every unsubscribe link already sent.</summary>
    public string UnsubscribeTokenSecret { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 signing secret for open-tracking pixel tokens (see IOpenTrackingTokenService).
    /// Deliberately separate from <see cref="UnsubscribeTokenSecret"/> so an open token can never be
    /// replayed as an unsubscribe token or the other way round. The API validates pixel tokens and the
    /// Worker mints them, so both processes must be configured with the same value; rotating it makes
    /// every pixel already sent stop counting.</summary>
    public string OpenTrackingSecret { get; init; } = string.Empty;

    /// <summary>Base URL of this API — used to build the per-recipient unsubscribe link embedded in
    /// campaign emails (<c>{ApiBaseUrl}/api/public/unsubscribe/{token}</c>).</summary>
    public string ApiBaseUrl { get; init; } = string.Empty;

    /// <summary>Base URL of the web app — the unsubscribe link inside campaign emails opens the creator-branded page
    /// <c>{FrontendBaseUrl}/unsubscribe/{token}</c>. When unset, the hosts fall back to <c>Email:FrontendBaseUrl</c>
    /// (the same app). Settable so that fallback can be applied after binding.</summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>How often <c>ScheduledCampaignDispatchWorker</c> polls for due Scheduled campaigns.</summary>
    public int ScheduledCampaignPollIntervalSeconds { get; init; } = 30;
}
