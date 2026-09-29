using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <summary>Stateless HMAC-signed open-tracking token: base64url(payload) + "." + base64url(HMAC-SHA256(purpose
/// + payload)), payload = "{recipientId}". Same shape as <see cref="UnsubscribeTokenService"/>, but signed
/// with its own secret and a purpose prefix, so the two token kinds are never interchangeable. No database
/// lookup is needed to validate a token — the signature alone proves it wasn't tampered with.</summary>
public sealed class OpenTrackingTokenService : IOpenTrackingTokenService
{
    private const string Purpose = "open-pixel|";

    private readonly byte[] _secretBytes;
    private readonly string _apiBaseUrl;

    public OpenTrackingTokenService(IOptions<MarketingOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.OpenTrackingSecret))
            throw new InvalidOperationException(
                "Marketing:OpenTrackingSecret is required — an empty secret would let anyone forge " +
                "open-tracking tokens and inflate any campaign's open count.");

        _secretBytes = Encoding.UTF8.GetBytes(options.Value.OpenTrackingSecret);
        _apiBaseUrl = options.Value.ApiBaseUrl.TrimEnd('/');
    }

    public string BuildPixelUrl(int recipientId)
    {
        return $"{_apiBaseUrl}/api/public/o/{Create(recipientId)}.gif";
    }

    public string Create(int recipientId)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(recipientId.ToString(CultureInfo.InvariantCulture));

        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(Sign(payloadBytes))}";
    }

    public int? TryParse(string token)
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

        if (!int.TryParse(Encoding.UTF8.GetString(payloadBytes), NumberStyles.None, CultureInfo.InvariantCulture, out var recipientId))
            return null;

        return recipientId;
    }

    private byte[] Sign(byte[] payloadBytes)
    {
        var purposeBytes = Encoding.UTF8.GetBytes(Purpose);
        var message = new byte[purposeBytes.Length + payloadBytes.Length];
        purposeBytes.CopyTo(message, 0);
        payloadBytes.CopyTo(message, purposeBytes.Length);

        return HMACSHA256.HashData(_secretBytes, message);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var padding = padded.Length % 4;
        if (padding != 0)
            padded += new string('=', 4 - padding);

        return Convert.FromBase64String(padded);
    }
}
