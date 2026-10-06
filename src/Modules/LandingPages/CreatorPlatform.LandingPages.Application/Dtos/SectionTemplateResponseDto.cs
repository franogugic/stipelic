namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class SectionTemplateResponseDto
{
    public string Key { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Variant { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ContentJson { get; init; } = string.Empty;
    /// <summary>Null = the page default.</summary>
    public string? DefaultBackgroundColor { get; init; }
    /// <summary>Navbar and Footer: always on the page, first / last, never added or removed.</summary>
    public bool IsLocked { get; init; }
}
