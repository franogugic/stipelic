using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record LoginUserRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;
}
