namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreatorSettingsResponseDto
{
    public Guid CreatorPublicId { get; init; }

    public string CreatorName { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string DefaultCurrency { get; init; } = string.Empty;

    public string SupportEmail { get; init; } = string.Empty;

    public string BrandName { get; init; } = string.Empty;

    public string LogoUrl { get; init; } = string.Empty;

    public string PrimaryColor { get; init; } = string.Empty;

    public string Timezone { get; init; } = string.Empty;

    public string Language { get; init; } = string.Empty;
}
