using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Analytics.Application.Dtos;

public sealed class EmailCaptureRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;
}
