using System.Security.Cryptography;
using System.Text;

namespace CreatorPlatform.Api.RateLimiting;

/// <summary>The (client IP, email) partition a login attempt is counted in. The email is hashed so no address ever
/// appears in a Redis key: SHA-256 of the trimmed, lower-cased address, hex, first 32 characters.</summary>
public static class LoginAttemptPartition
{
    public static string For(string ipAddress, string email) => $"{ipAddress}:{HashEmail(email)}";

    public static string HashEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash)[..32];
    }
}
