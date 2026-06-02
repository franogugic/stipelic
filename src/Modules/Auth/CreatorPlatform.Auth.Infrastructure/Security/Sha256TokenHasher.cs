using System.Security.Cryptography;
using System.Text;
using CreatorPlatform.Auth.Application.Interfaces;

namespace CreatorPlatform.Auth.Infrastructure.Security;

public sealed class Sha256TokenHasher : ITokenHasher
{
    public string Hash(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(tokenBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
