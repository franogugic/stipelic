namespace CreatorPlatform.Auth.Application.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string SessionCookieName { get; init; } = "creator_platform_session";

    public int SessionLifetimeDays { get; init; } = 7;

    public bool SessionCookieSecure { get; init; } = true;

    public string SessionCookieSameSite { get; init; } = "None";

    public string? SessionCookieDomain { get; init; }
}
