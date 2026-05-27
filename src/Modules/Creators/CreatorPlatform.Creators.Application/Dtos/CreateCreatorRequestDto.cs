using System.ComponentModel.DataAnnotations;

namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreateCreatorRequestDto
{
    [Required]
    public string Name { get; init; } = string.Empty;
    
    [Required]
    public string Slug { get; init; } = string.Empty;
    
    [Required]
    public string PlanCode { get; init; } = string.Empty;

    public string DefaultCurrency { get; init; } = "EUR";

    public string? SupportEmail { get; init; }

    public string? BrandName { get; init; }

    public string? LogoUrl { get; init; }

    public string? PrimaryColor { get; init; }

    public string? Timezone { get; init; }
}
