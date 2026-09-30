namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IOpenTrackingTokenService
{
    /// <summary>Builds a stateless, tamper-evident open-tracking token for one campaign recipient.</summary>
    string Create(int recipientId);

    /// <summary>Validates the token's HMAC signature (constant-time comparison) and returns the recipient
    /// id. Returns null for any malformed, tampered, or unparseable token — never throws, since this is
    /// called on public, unauthenticated input.</summary>
    int? TryParse(string token);

    /// <summary>Full tracking-pixel URL (API base + <see cref="Create"/> token) for a recipient — the exact
    /// link embedded in campaign emails, so callers never need to know the API's base URL or route.</summary>
    string BuildPixelUrl(int recipientId);
}
