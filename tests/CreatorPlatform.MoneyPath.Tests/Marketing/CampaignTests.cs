using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class CampaignTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Campaign CreateValidLandingPageDraft() => Campaign.CreateDraft(
        creatorId: 1,
        subject: "Big sale!",
        bodyText: "Check out our new product.",
        ctaLabel: null,
        ctaUrl: null,
        audienceType: CampaignAudienceType.LandingPage,
        landingPageId: 10,
        productId: null,
        createdAt: Now);

    [Fact]
    public void CreateDraft_ValidLandingPageAudience_Succeeds()
    {
        var campaign = CreateValidLandingPageDraft();

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(CampaignAudienceType.LandingPage, campaign.AudienceType);
        Assert.Equal(10, campaign.LandingPageId);
        Assert.Null(campaign.ProductId);
        Assert.Equal(0, campaign.RecipientCount);
    }

    [Fact]
    public void CreateDraft_ValidProductAudience_Succeeds()
    {
        var campaign = Campaign.CreateDraft(
            1, "Subject", "Body", null, null, CampaignAudienceType.Product, null, 5, Now);

        Assert.Equal(CampaignAudienceType.Product, campaign.AudienceType);
        Assert.Equal(5, campaign.ProductId);
        Assert.Null(campaign.LandingPageId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_EmptySubject_Throws(string subject)
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, subject, "Body", null, null, CampaignAudienceType.LandingPage, 10, null, Now));
    }

    [Fact]
    public void CreateDraft_SubjectTooLong_Throws()
    {
        var subject = new string('a', Campaign.MaxSubjectLength + 1);

        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, subject, "Body", null, null, CampaignAudienceType.LandingPage, 10, null, Now));
    }

    [Fact]
    public void CreateDraft_BodyTextTooLong_Throws()
    {
        var body = new string('a', Campaign.MaxBodyTextLength + 1);

        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, "Subject", body, null, null, CampaignAudienceType.LandingPage, 10, null, Now));
    }

    [Fact]
    public void CreateDraft_CtaLabelWithoutUrl_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, "Subject", "Body", "Buy now", null, CampaignAudienceType.LandingPage, 10, null, Now));
    }

    [Fact]
    public void CreateDraft_CtaUrlWithoutLabel_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, "Subject", "Body", null, "https://example.com", CampaignAudienceType.LandingPage, 10, null, Now));
    }

    [Fact]
    public void CreateDraft_CtaLabelAndUrlBothSet_Succeeds()
    {
        var campaign = Campaign.CreateDraft(
            1, "Subject", "Body", "Buy now", "https://example.com", CampaignAudienceType.LandingPage, 10, null, Now);

        Assert.Equal("Buy now", campaign.CtaLabel);
        Assert.Equal("https://example.com", campaign.CtaUrl);
    }

    [Fact]
    public void CreateDraft_LandingPageAudienceWithProductIdSet_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, "Subject", "Body", null, null, CampaignAudienceType.LandingPage, 10, 5, Now));
    }

    [Fact]
    public void CreateDraft_LandingPageAudienceWithoutLandingPageId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, "Subject", "Body", null, null, CampaignAudienceType.LandingPage, null, null, Now));
    }

    [Fact]
    public void CreateDraft_ProductAudienceWithLandingPageIdSet_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, "Subject", "Body", null, null, CampaignAudienceType.Product, 10, 5, Now));
    }

    [Fact]
    public void CreateDraft_ProductAudienceWithoutProductId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateDraft(
            1, "Subject", "Body", null, null, CampaignAudienceType.Product, null, null, Now));
    }

    [Fact]
    public void UpdateDraft_WhileDraft_Succeeds()
    {
        var campaign = CreateValidLandingPageDraft();

        campaign.UpdateDraft("New subject", "New body", "Buy", "https://example.com", CampaignAudienceType.LandingPage, 10, null, Now);

        Assert.Equal("New subject", campaign.Subject);
        Assert.Equal("New body", campaign.BodyText);
        Assert.Equal("Buy", campaign.CtaLabel);
    }

    [Fact]
    public void UpdateDraft_AfterQueued_Throws()
    {
        var campaign = CreateValidLandingPageDraft();
        campaign.MarkQueued(5, Now);

        Assert.Throws<InvalidOperationException>(() => campaign.UpdateDraft(
            "New subject", "New body", null, null, CampaignAudienceType.LandingPage, 10, null, Now));
    }

    [Fact]
    public void MarkQueued_FromDraft_Succeeds()
    {
        var campaign = CreateValidLandingPageDraft();

        campaign.MarkQueued(42, Now);

        Assert.Equal(CampaignStatus.Queued, campaign.Status);
        Assert.Equal(42, campaign.RecipientCount);
        Assert.Equal(Now, campaign.QueuedAt);
    }

    [Fact]
    public void MarkQueued_AlreadyQueued_Throws()
    {
        var campaign = CreateValidLandingPageDraft();
        campaign.MarkQueued(5, Now);

        Assert.Throws<InvalidOperationException>(() => campaign.MarkQueued(10, Now));
    }

    [Fact]
    public void MarkQueued_ZeroRecipientCount_Throws()
    {
        var campaign = CreateValidLandingPageDraft();

        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.MarkQueued(0, Now));
    }

    [Fact]
    public void MarkQueued_NegativeRecipientCount_Throws()
    {
        var campaign = CreateValidLandingPageDraft();

        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.MarkQueued(-1, Now));
    }
}
