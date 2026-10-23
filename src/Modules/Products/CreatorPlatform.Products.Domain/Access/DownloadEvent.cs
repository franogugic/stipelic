namespace CreatorPlatform.Products.Domain.Access;

/// <summary>
/// One download through a personal link (R6.2): written by the download endpoint in the same statement that bumps the
/// link's counter, so only a working link to a product with a file is ever an event. Every request counts, repeats
/// included; analytics bucket them on the day they happen. <see cref="LandingPageId"/> is the page the link came from
/// (the sign-up's page, or the page the order was placed on), null when there was none.
/// </summary>
public sealed class DownloadEvent
{
    private DownloadEvent()
    {
    }

    public long Id { get; private set; }

    public int GrantId { get; private set; }

    public int CreatorId { get; private set; }

    public int ProductId { get; private set; }

    public int? LandingPageId { get; private set; }

    public DateTimeOffset DownloadedAt { get; private set; }
}
