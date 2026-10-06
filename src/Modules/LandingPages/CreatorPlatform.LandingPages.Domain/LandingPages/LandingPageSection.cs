namespace CreatorPlatform.LandingPages.Domain.LandingPages;

public sealed class LandingPageSection
{
    private LandingPageSection()
    {
    }

    private LandingPageSection(
        Guid publicId,
        LandingPage landingPage,
        LandingPageSectionType type,
        string variant,
        int sortOrder,
        string? backgroundColor,
        string contentJson,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        LandingPage = landingPage;
        Type = type;
        Variant = variant;
        SortOrder = sortOrder;
        BackgroundColor = backgroundColor;
        ContentJson = contentJson;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static LandingPageSection Create(
        LandingPage landingPage,
        LandingPageSectionType type,
        string variant,
        int sortOrder,
        string? backgroundColor,
        string contentJson,
        DateTimeOffset createdAt)
    {
        return new LandingPageSection(
            Guid.NewGuid(),
            landingPage,
            type,
            variant,
            sortOrder,
            backgroundColor,
            contentJson,
            createdAt);
    }

    public void Update(
        string variant,
        int sortOrder,
        string? backgroundColor,
        string contentJson,
        DateTimeOffset updatedAt)
    {
        Variant = variant;
        SortOrder = sortOrder;
        BackgroundColor = backgroundColor;
        ContentJson = contentJson;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int LandingPageId { get; private set; }

    public LandingPage LandingPage { get; private set; } = null!;

    public LandingPageSectionType Type { get; private set; }

    /// <summary>The layout of the section, one of the type's variants in <c>SectionTemplates</c>.</summary>
    public string Variant { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    /// <summary>A hex colour, or null for the page default (follows the page's light / dark theme).</summary>
    public string? BackgroundColor { get; private set; }

    public string ContentJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
