namespace CreatorPlatform.Marketing.Domain.Campaigns;

public enum CampaignAudienceType
{
    LandingPage,
    Product,

    /// <summary>Every non-unsubscribed contact of the creator — targets no landing page or product, so both
    /// ids are null.</summary>
    All,

    /// <summary>A resend of a sent campaign to the people it reached who have opened neither it nor any earlier
    /// resend of it — <see cref="Campaign.SourceCampaignId"/> points at the original (never at another resend),
    /// landing page and product ids are null.</summary>
    CampaignNonOpeners
}
