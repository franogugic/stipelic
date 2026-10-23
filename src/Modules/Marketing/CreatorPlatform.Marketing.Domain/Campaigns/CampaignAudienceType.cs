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
    LandingPageBuyers
}
