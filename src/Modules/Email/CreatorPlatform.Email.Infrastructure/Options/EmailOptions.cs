namespace CreatorPlatform.Email.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string ConnectionString { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FrontendBaseUrl { get; init; } = string.Empty;

    /// <summary>How long to push a message's processing lease out by when the provider throttles a send
    /// (e.g. ACS 429) — the message is retried after this delay without consuming a retry attempt.</summary>
    public int ThrottleBackoffMinutes { get; init; } = 15;
}
