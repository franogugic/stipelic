namespace CreatorPlatform.Analytics.Domain.PageViews;

public sealed class PageView
{
    public Guid Id { get; init; }

    public int LandingPageId { get; init; }

    public Guid VisitorId { get; init; }

    public DateTimeOffset ViewedAt { get; init; }

    // UTC calendar day — used for unique constraint deduplication
    public DateOnly ViewedDate { get; init; }
}
