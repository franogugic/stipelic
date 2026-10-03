using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record RequestEmailChangeRequestDto
{
    [Required]
    public string NewEmail { get; init; } = string.Empty;

    [Required]
    public string CurrentPassword { get; init; } = string.Empty;
}

/// <summary>The same 202 whether a link was sent or the address is already taken — it never reveals whether an
/// account exists.</summary>
public sealed record RequestEmailChangeResponseDto
{
    public string Message { get; init; } = string.Empty;
}

/// <remarks>No <c>[Required]</c>: a missing or blank token answers the same 400 as an unknown one.</remarks>
public sealed record ConfirmEmailChangeRequestDto
{
    public string? Token { get; init; }
}

public sealed record ConfirmEmailChangeResponseDto
{
    public string Message { get; init; } = string.Empty;

    /// <summary>The account's new sign-in email.</summary>
    public string Email { get; init; } = string.Empty;
}
