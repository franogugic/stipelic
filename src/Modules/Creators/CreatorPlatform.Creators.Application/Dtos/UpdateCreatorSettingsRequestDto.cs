using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class UpdateCreatorSettingsRequestDto
{
    [MaxLength(100)]
    [EmailAddress]
    public string? SupportEmail { get; init; }

    [MaxLength(50)]
    public string? BrandName { get; init; }

    [MaxLength(500)]
    [Url]
    public string? LogoUrl { get; init; }

    [MaxLength(7)]
    [RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string? PrimaryColor { get; init; }

    [MaxLength(50)]
    public string? Timezone { get; init; }

    [MaxLength(10)]
    [RegularExpression("^[a-z]{2}(-[a-z]{2})?$")]
    public string? Language { get; init; }
}
