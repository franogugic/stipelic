namespace CreatorPlatform.Marketing.Domain.Campaigns;

/// <summary>Where a tracked link sits in a campaign email.</summary>
public enum CampaignLinkKind
{
    /// <summary>The call-to-action button; its label is the CTA label.</summary>
    Button,

    /// <summary>A URL in the body text, linked as itself (its visible text is the URL).</summary>
    Text
}
