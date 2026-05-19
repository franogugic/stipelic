using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record RegisterUserRequestDto
{
    [Required]
    [MaxLength(50)]
    [MinLength(2)]
    public string FirstName { get; init; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    [MinLength(2)]
    public string LastName { get; init; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; init; } = string.Empty;
    
    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;
}