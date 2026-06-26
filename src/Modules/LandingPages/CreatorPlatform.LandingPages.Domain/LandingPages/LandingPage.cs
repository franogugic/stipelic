namespace CreatorPlatform.LandingPages.Domain.LandingPages;

public sealed class LandingPage
{
    private LandingPage()
    {
    }

    private LandingPage(
        Guid publicId,
        int creatorId,
        int? productId,
        string title,
        string slug,
        LandingPageType type,
        LandingPageStatus status,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        ProductId = productId;
        Title = title;
        Slug = slug;
        Type = type;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static LandingPage Create(
        int creatorId,
        int? productId,
        string title,
        string slug,
        LandingPageType type,
        DateTimeOffset createdAt)
    {
        return new LandingPage(
            Guid.NewGuid(),
            creatorId,
            productId,
            title,
            slug,
            type,
            LandingPageStatus.Draft,
            createdAt);
    }

    public void Update(
        int? productId,
        string title,
        string slug,
        LandingPageType type,
        DateTimeOffset updatedAt)
    {
        ProductId = productId;
        Title = title;
        Slug = slug;
        Type = type;
        UpdatedAt = updatedAt;
    }

    public void Publish(DateTimeOffset updatedAt)
    {
        Status = LandingPageStatus.Published;
        UpdatedAt = updatedAt;
    }

    public void Unpublish(DateTimeOffset updatedAt)
    {
        Status = LandingPageStatus.Draft;
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset updatedAt)
    {
        Status = LandingPageStatus.Archived;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public int? ProductId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public LandingPageType Type { get; private set; }

    public LandingPageStatus Status { get; private set; }

    public string? CustomDomain { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
