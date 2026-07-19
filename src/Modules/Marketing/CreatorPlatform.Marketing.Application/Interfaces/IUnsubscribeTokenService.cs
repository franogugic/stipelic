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

    /// <summary>Full one-click unsubscribe URL (API base + <see cref="Create"/> token) for a recipient —
    /// the exact link embedded in campaign emails, so callers never need to know the API's base URL.</summary>
    string BuildUnsubscribeUrl(int creatorId, string email);
}
