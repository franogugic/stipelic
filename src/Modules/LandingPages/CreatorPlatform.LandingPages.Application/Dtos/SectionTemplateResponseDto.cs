namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class SectionTemplateResponseDto
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string ContentJson { get; init; } = string.Empty;
    public string DefaultBackgroundColor { get; init; } = string.Empty;
}
