using System.Text.Json.Serialization;

namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record InspectPasswordResetTokenResponseDto
{
    /// <summary><see cref="PasswordResetTokenStatus.Valid"/> or <see cref="PasswordResetTokenStatus.Expired"/>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>The account's address, only when <see cref="Status"/> is Valid — omitted from the JSON otherwise,
    /// so a dead link never reveals whose it was.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }
}

public static class PasswordResetTokenStatus
{
    public const string Valid = "Valid";

    /// <summary>Used or expired — deliberately one status, never distinguishing the two.</summary>
    public const string Expired = "Expired";
}
