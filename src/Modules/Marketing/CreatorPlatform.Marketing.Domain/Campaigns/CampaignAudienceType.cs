namespace CreatorPlatform.Marketing.Domain.Campaigns;

public enum CampaignAudienceType
{
    LandingPage,
    Product,

    /// <summary>Every non-unsubscribed contact of the creator — targets no landing page or product, so both
    /// ids are null.</summary>
    All,

    /// <summary>Everyone with a paid order through one landing page (sets <c>LandingPageId</c>, leaves
    /// <c>ProductId</c> null). Marketing to existing customers relies on the soft opt-in, so only Paid orders count
    /// and the footer says the recipient is receiving the email because they bought.</summary>
    LandingPageBuyers,

    /// <summary>A resend of a sent campaign to the people it reached who have opened neither it nor any earlier
    /// resend of it — <see cref="Campaign.SourceCampaignId"/> points at the original (never at another resend),
    /// landing page and product ids are null.</summary>
    CampaignNonOpeners
}
