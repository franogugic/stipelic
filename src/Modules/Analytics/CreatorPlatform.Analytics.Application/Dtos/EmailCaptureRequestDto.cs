using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Analytics.Application.Dtos;

public sealed class EmailCaptureRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(254)]
    public string Email { get; init; } = string.Empty;

    /// <summary>The honeypot: a field people never see, so it is always empty from a person. When a script fills it in,
    /// the sign-up answers like a success and does nothing. No length limit, so a bot never learns from a 400.</summary>
    public string? Website { get; init; }
}
