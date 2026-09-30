namespace CreatorPlatform.Marketing.Domain.Campaigns;

public enum CampaignAudienceType
{
    LandingPage,
    Product,

    /// <summary>Every non-unsubscribed contact of the creator — targets no landing page or product, so both
    /// ids are null.</summary>
    All
}
