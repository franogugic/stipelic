namespace CreatorPlatform.Auth.Application.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string SessionCookieName { get; init; } = "creator_platform_session";

    public int SessionLifetimeDays { get; init; } = 7;

    public bool SessionCookieSecure { get; init; } = true;

    /// <summary>Lax: the cookie never rides a cross-site POST/DELETE (CSRF). The web app and the API must therefore share
    /// one registrable domain (e.g. <c>app.luma.hr</c> and <c>api.luma.hr</c>). Only "None" (with Secure) would allow a
    /// cross-site setup, and then the anti-forgery header is the only CSRF guard left.</summary>
    public string SessionCookieSameSite { get; init; } = "Lax";

    public string? SessionCookieDomain { get; init; }
}
