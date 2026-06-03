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
        int sortOrder,
        string backgroundColor,
        string contentJson,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        LandingPage = landingPage;
        Type = type;
        SortOrder = sortOrder;
        BackgroundColor = backgroundColor;
        ContentJson = contentJson;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static LandingPageSection Create(
        LandingPage landingPage,
        LandingPageSectionType type,
        int sortOrder,
        string backgroundColor,
        string contentJson,
        DateTimeOffset createdAt)
    {
        return new LandingPageSection(
            Guid.NewGuid(),
            landingPage,
            type,
            sortOrder,
            backgroundColor,
            contentJson,
            createdAt);
    }

    public void Update(
        int sortOrder,
        string backgroundColor,
        string contentJson,
        DateTimeOffset updatedAt)
    {
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

    public int SortOrder { get; private set; }

    public string BackgroundColor { get; private set; } = string.Empty;

    public string ContentJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
