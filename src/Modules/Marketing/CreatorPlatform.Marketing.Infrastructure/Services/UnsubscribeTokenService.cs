using System.Security.Cryptography;
using System.Text;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

/// <summary>Stateless HMAC-signed unsubscribe token: base64url(payload) + "." + base64url(HMAC-SHA256(payload)),
/// payload = "{creatorId}|{email}" (email lowercased/trimmed before signing). No database lookup is
/// needed to validate a token — the signature alone proves it wasn't tampered with.</summary>
public sealed class UnsubscribeTokenService : IUnsubscribeTokenService
{
    private readonly byte[] _secretBytes;
    private readonly string _apiBaseUrl;

    public UnsubscribeTokenService(IOptions<MarketingOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.UnsubscribeTokenSecret))
            throw new InvalidOperationException(
                "Marketing:UnsubscribeTokenSecret is required — an empty secret would let anyone forge " +
                "unsubscribe tokens for any creator/email pair (mass-unsubscribe attack).");

        _secretBytes = Encoding.UTF8.GetBytes(options.Value.UnsubscribeTokenSecret);
        _apiBaseUrl = options.Value.ApiBaseUrl.TrimEnd('/');
    }

    public string BuildUnsubscribeUrl(int creatorId, string email)
    {
        var token = Create(creatorId, email);
        return $"{_apiBaseUrl}/api/public/unsubscribe/{token}";
    }

    public string Create(int creatorId, string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var payload = $"{creatorId}|{normalizedEmail}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signatureBytes = HMACSHA256.HashData(_secretBytes, payloadBytes);

        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signatureBytes)}";
    }

    public UnsubscribeTokenPayload? TryParse(string token)
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

        var expectedSignature = HMACSHA256.HashData(_secretBytes, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
            return null;

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var separatorIndex = payload.IndexOf('|');
        if (separatorIndex <= 0 || separatorIndex == payload.Length - 1)
            return null;

        if (!int.TryParse(payload[..separatorIndex], out var creatorId))
            return null;

        var email = payload[(separatorIndex + 1)..];
        return new UnsubscribeTokenPayload(creatorId, email);
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
