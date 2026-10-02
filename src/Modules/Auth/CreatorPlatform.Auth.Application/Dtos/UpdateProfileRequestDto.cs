using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Auth.Application.Dtos;

/// <summary>Same constraints as <see cref="RegisterUserRequestDto"/>'s name fields; the service trims and applies
/// the same character rules.</summary>
public sealed record UpdateProfileRequestDto
{
    [Required]
    [MaxLength(50)]
    [MinLength(2)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [MinLength(2)]
    public string LastName { get; init; } = string.Empty;
}
