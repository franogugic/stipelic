using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreateCreatorRequestDto
{
    [Required(AllowEmptyStrings = false)]
    [MinLength(2)]
    [MaxLength(50)]
    public string Name { get; init; } = string.Empty;
    
    [Required(AllowEmptyStrings = false)]
    [MinLength(3)]
    [MaxLength(50)]
    [RegularExpression("^[a-zA-Z0-9-]+$")]
    public string Slug { get; init; } = string.Empty;
    
    [Required(AllowEmptyStrings = false)]
    [MaxLength(20)]
    [RegularExpression("^[a-zA-Z0-9_-]+$")]
    public string PlanCode { get; init; } = string.Empty;

    [MaxLength(5)]
    [RegularExpression("^[A-Z]{3}$")]
    public string DefaultCurrency { get; init; } = "EUR";

    [EmailAddress]
    [MaxLength(100)]
    public string? SupportEmail { get; init; }

    [MaxLength(50)]
    public string? BrandName { get; init; }

    [Url]
    [MaxLength(500)]
    public string? LogoUrl { get; init; }

    [MaxLength(7)]
    [RegularExpression("^#[0-9a-fA-F]{6}$")]
    public string? PrimaryColor { get; init; }

    [MaxLength(50)]
    [RegularExpression("^[A-Za-z0-9_./+-]+$")]
    public string? Timezone { get; init; }

    [MaxLength(10)]
    [RegularExpression("^[a-zA-Z]{2}(-[a-zA-Z]{2})?$")]
    public string? Language { get; init; }
}
