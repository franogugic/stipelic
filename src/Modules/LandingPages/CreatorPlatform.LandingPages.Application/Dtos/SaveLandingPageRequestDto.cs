namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class SaveLandingPageRequestDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    /// <summary>A product to switch the page to; null keeps the current one.</summary>
    public Guid? ProductId { get; init; }
    /// <summary>A lead page stops giving a document away (a plain newsletter sign-up). Ignored when
    /// <see cref="ProductId"/> is set.</summary>
    public bool RemoveProduct { get; init; }
    public List<SaveLandingPageSectionDto> Sections { get; init; } = [];
}

public sealed class SaveLandingPageSectionDto
{
    /// <summary>Existing section PublicId. Null means new section.</summary>
    public Guid? PublicId { get; init; }
    public string Type { get; init; } = string.Empty;
    /// <summary>One of the type's variants. Empty keeps an existing section's variant, or gives a new section
    /// the type's default.</summary>
    public string? Variant { get; init; }
    public int SortOrder { get; init; }
    /// <summary>A hex colour; null or empty means the page default.</summary>
    public string? BackgroundColor { get; init; }
    public string ContentJson { get; init; } = string.Empty;
}
