using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <summary>Stateless HMAC-signed tracked-link token: base64url(payload) + "." + base64url(HMAC-SHA256(purpose +
/// payload)), payload = "{recipientId}:{linkId}". Same shape as <see cref="OpenTrackingTokenService"/>, with its own
/// secret and purpose prefix, so no other token kind is accepted here. A token is validated without the database;
/// it only says which recipient's copy of which link was clicked — the destination always comes from the database.</summary>
public sealed class ClickTrackingTokenService : IClickTrackingTokenService
{
    private const string Purpose = "link-click|";

    private readonly byte[] _secretBytes;
    private readonly string _apiBaseUrl;

    public ClickTrackingTokenService(IOptions<MarketingOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ClickTrackingSecret))
            throw new InvalidOperationException(
                "Marketing:ClickTrackingSecret is required — an empty secret would let anyone forge tracked-link " +
                "tokens and inflate any campaign's click counts.");

        _secretBytes = Encoding.UTF8.GetBytes(options.Value.ClickTrackingSecret);
        _apiBaseUrl = options.Value.ApiBaseUrl.TrimEnd('/');
    }

    public string BuildTrackedUrl(int recipientId, int linkId) =>
        $"{_apiBaseUrl}/api/public/c/{Create(recipientId, linkId)}";

    public string Create(int recipientId, int linkId)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{recipientId}:{linkId}"));

        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(Sign(payloadBytes))}";
    }

    public ClickTokenPayload? TryParse(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
            return null;

        byte[] payloadBytes;
        byte[] signatureBytes;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            signatureBytes = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(signatureBytes, Sign(payloadBytes)))
            return null;

        var ids = Encoding.UTF8.GetString(payloadBytes).Split(':');
        if (ids.Length != 2
            || !int.TryParse(ids[0], NumberStyles.None, CultureInfo.InvariantCulture, out var recipientId)
            || !int.TryParse(ids[1], NumberStyles.None, CultureInfo.InvariantCulture, out var linkId))
            return null;

        return new ClickTokenPayload(recipientId, linkId);
    }

    private byte[] Sign(byte[] payloadBytes)
    {
        var purposeBytes = Encoding.UTF8.GetBytes(Purpose);
        var message = new byte[purposeBytes.Length + payloadBytes.Length];
        purposeBytes.CopyTo(message, 0);
        payloadBytes.CopyTo(message, purposeBytes.Length);

        return HMACSHA256.HashData(_secretBytes, message);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var padding = padded.Length % 4;
        if (padding != 0)
            padded += new string('=', 4 - padding);

        return Convert.FromBase64String(padded);
    }
}
