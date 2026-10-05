namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record UnsubscribeTokenPayload(int CreatorId, string Email);

public interface IUnsubscribeTokenService
{
    /// <summary>Builds a stateless, tamper-evident unsubscribe token for a creator/email pair. Email is
    /// normalized (trimmed + lowercased) before signing, so <see cref="TryParse"/> always returns the
    /// normalized form regardless of how the recipient's address was capitalized at capture time.</summary>
    string Create(int creatorId, string email);

    /// <summary>Validates the token's HMAC signature (constant-time comparison) and decodes its payload.
    /// Returns null for any malformed, tampered, or unparseable token — never throws, since this is
    /// called on public, unauthenticated input.</summary>
    UnsubscribeTokenPayload? TryParse(string token);

    /// <summary>The RFC 8058 one-click URL (API base + token) for the <c>List-Unsubscribe</c> header: mail clients POST
    /// to it without any UI.</summary>
    string BuildUnsubscribeUrl(int creatorId, string email);

    /// <summary>The creator-branded unsubscribe page in the web app (<c>{FrontendBaseUrl}/unsubscribe/{token}</c>) — the
    /// link people click inside the email. Opening it changes nothing; the page asks for a confirmation.</summary>
    string BuildUnsubscribePageUrl(int creatorId, string email);

    /// <summary>The same page for an existing token — where the old GET link redirects.</summary>
    string BuildUnsubscribePageUrl(string token);
}
