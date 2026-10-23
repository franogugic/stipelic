namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record ClickTokenPayload(int RecipientId, int LinkId);

public interface IClickTrackingTokenService
{
    /// <summary>A stateless, tamper-evident token for one recipient's copy of one link.</summary>
    string Create(int recipientId, int linkId);

    /// <summary>Checks the HMAC signature (constant-time) and returns the ids; null for anything malformed or
    /// tampered with. Never throws: it reads public, unauthenticated input.</summary>
    ClickTokenPayload? TryParse(string token);

    /// <summary>The tracked URL put in the email: <c>{ApiBaseUrl}/api/public/c/{token}</c>.</summary>
    string BuildTrackedUrl(int recipientId, int linkId);
}
