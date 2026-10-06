namespace CreatorPlatform.Email.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string ConnectionString { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FrontendBaseUrl { get; init; } = string.Empty;

    /// <summary>Absolute URL of the hosted Luma mark (PNG, shown at 28 px) in system emails. Empty → the text wordmark
    /// only. Development: <c>{FrontendBaseUrl}/email/luma-mark-112.png</c>.</summary>
    public string LogoUrl { get; init; } = string.Empty;

    /// <summary>Footer "Help" link of system emails; omitted while empty.</summary>
    public string HelpUrl { get; init; } = string.Empty;

    /// <summary>Footer "Privacy" link of system emails; omitted while empty.</summary>
    public string PrivacyUrl { get; init; } = string.Empty;

    /// <summary>Luma's support address, named in security notices; omitted while empty.</summary>
    public string SupportEmail { get; init; } = string.Empty;

    /// <summary>How long to push a message's processing lease out by when the provider throttles a send
    /// (e.g. ACS 429) — the message is retried after this delay without consuming a retry attempt.</summary>
    public int ThrottleBackoffMinutes { get; init; } = 15;
}
