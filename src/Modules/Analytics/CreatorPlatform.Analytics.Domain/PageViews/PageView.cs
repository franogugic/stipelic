namespace CreatorPlatform.Analytics.Domain.PageViews;

public sealed class PageView
{
    private PageView()
    {
    }

    private PageView(
        Guid id,
        int landingPageId,
        Guid visitorId,
        DateTimeOffset viewedAt)
    {
        Id = id;
        LandingPageId = landingPageId;
        VisitorId = visitorId;
        ViewedAt = viewedAt;
    }

    public static PageView Record(
        int landingPageId,
        Guid visitorId,
        DateTimeOffset viewedAt)
    {
        return new PageView(
            Guid.NewGuid(),
            landingPageId,
            visitorId,
            viewedAt);
    }

    public Guid Id { get; private set; }

    public int LandingPageId { get; private set; }

    public Guid VisitorId { get; private set; }

    public DateTimeOffset ViewedAt { get; private set; }
}
