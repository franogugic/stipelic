namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class LandingPageSectionResponseDto
{
    public Guid PublicId { get; init; }
    public string Type { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string BackgroundColor { get; init; } = string.Empty;
    public string ContentJson { get; init; } = string.Empty;
    public bool IsLocked { get; init; }
}
