using System.Security.Cryptography;
using CreatorPlatform.Auth.Application.Interfaces;
using Microsoft.AspNetCore.WebUtilities;

namespace CreatorPlatform.Auth.Infrastructure.Security;

public sealed class TokenGenerator : ITokenGenerator
{
    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);

        return WebEncoders.Base64UrlEncode(bytes);
    } 
}
