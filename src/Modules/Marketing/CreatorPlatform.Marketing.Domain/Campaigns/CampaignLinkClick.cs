namespace CreatorPlatform.Marketing.Domain.Campaigns;

/// <summary>One recipient's clicks on one link. Written only by the click endpoint's single upsert statement, so
/// there is no factory and no mutator.</summary>
public sealed class CampaignLinkClick
{
    private CampaignLinkClick()
    {
    }

    public int LinkId { get; private set; }

    public int RecipientId { get; private set; }

    public DateTimeOffset FirstClickedAt { get; private set; }

    public DateTimeOffset LastClickedAt { get; private set; }

    public int ClickCount { get; private set; }
}
