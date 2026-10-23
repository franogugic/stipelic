namespace CreatorPlatform.Marketing.Domain.Campaigns;

/// <summary>One distinct link of a sent campaign, created in the send transaction. The click counters are only
/// changed by the click endpoint's raw SQL (one statement per click), never through this type. Link scanners can
/// pre-click links, so clicks over-report somewhat.</summary>
public sealed class CampaignLink
{
    private CampaignLink()
    {
    }

    private CampaignLink(int campaignId, short position, string url, CampaignLinkKind kind, string? label, DateTimeOffset createdAt)
    {
        CampaignId = campaignId;
        Position = position;
        Url = url;
        Kind = kind;
        Label = label;
        CreatedAt = createdAt;
    }

    public static CampaignLink Create(
        int campaignId, int position, string url, CampaignLinkKind kind, string? label, DateTimeOffset createdAt)
    {
        if (position < 1 || position > short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(position), position, "Position must be positive.");
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required.", nameof(url));
        if (kind == CampaignLinkKind.Button == string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("A button link has a label; a text link has none.", nameof(label));

        return new CampaignLink(campaignId, (short)position, url, kind, label, createdAt);
    }

    public int Id { get; private set; }

    public int CampaignId { get; private set; }

    /// <summary>Order of first appearance in the email, from 1.</summary>
    public short Position { get; private set; }

    /// <summary>The destination — the only source of a click's redirect target.</summary>
    public string Url { get; private set; } = string.Empty;

    public CampaignLinkKind Kind { get; private set; }

    /// <summary>The CTA label for <see cref="CampaignLinkKind.Button"/>; null for text links.</summary>
    public string? Label { get; private set; }

    /// <summary>Recipients who clicked this link at least once.</summary>
    public int UniqueClickCount { get; private set; }

    public int TotalClickCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
