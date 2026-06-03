namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class SaveLandingPageRequestDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public List<SaveLandingPageSectionDto> Sections { get; init; } = [];
}

public sealed class SaveLandingPageSectionDto
{
    /// <summary>Existing section PublicId. Null means new section.</summary>
    public Guid? PublicId { get; init; }
    public string Type { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string BackgroundColor { get; init; } = string.Empty;
    public string ContentJson { get; init; } = string.Empty;
}
