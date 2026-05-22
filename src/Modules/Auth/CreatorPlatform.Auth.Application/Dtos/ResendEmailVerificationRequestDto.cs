using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record ResendEmailVerificationRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; init; } = string.Empty;
}
