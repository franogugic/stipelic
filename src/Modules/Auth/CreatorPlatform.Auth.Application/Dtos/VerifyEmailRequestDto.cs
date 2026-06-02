using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Auth.Application.Dtos;

public sealed record VerifyEmailRequestDto
{
    [Required]
    public string Token { get; init; } = string.Empty;
}
