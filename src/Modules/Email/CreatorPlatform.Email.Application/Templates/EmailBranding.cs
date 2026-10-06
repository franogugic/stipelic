namespace CreatorPlatform.Email.Application.Templates;

/// <summary>Luma-wide email settings (<c>Email:*</c>). Empty values leave their element out: the mark falls back to the
/// text wordmark, and the Help / Privacy links and the support address are omitted.</summary>
public sealed record EmailBranding(string? LogoUrl, string? HelpUrl, string? PrivacyUrl, string? SupportEmail)
{
    public static EmailBranding None { get; } = new(null, null, null, null);
}

/// <summary>A rendered email: subject, HTML and plain-text bodies.</summary>
public sealed record RenderedEmail(string Subject, string Html, string PlainText);
