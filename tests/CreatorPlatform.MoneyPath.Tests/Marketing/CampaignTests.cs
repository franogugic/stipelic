using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Mail;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class CampaignTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Campaign CreateValidLandingPageSend(int recipientCount = 42) => Campaign.CreateQueuedFromTemplate(
        creatorId: 1,
        templateId: 7,
        subject: "Big sale!",
        bodyText: "Check out our new product.",
        ctaLabel: null,
        ctaUrl: null,
        audienceType: CampaignAudienceType.LandingPage,
        landingPageId: 10,
        productId: null,
        recipientCount: recipientCount,
        queuedAt: Now);

    [Fact]
    public void CreateQueuedFromTemplate_ValidLandingPageAudience_Succeeds()
    {
        var campaign = CreateValidLandingPageSend();

        Assert.Equal(CampaignStatus.Queued, campaign.Status);
        Assert.Equal(7, campaign.TemplateId);
        Assert.Equal(CampaignAudienceType.LandingPage, campaign.AudienceType);
        Assert.Equal(10, campaign.LandingPageId);
        Assert.Null(campaign.ProductId);
        Assert.Equal(42, campaign.RecipientCount);
        Assert.Equal(Now, campaign.QueuedAt);
    }

    [Fact]
    public void CreateQueuedFromTemplate_ValidProductAudience_Succeeds()
    {
        var campaign = Campaign.CreateQueuedFromTemplate(
            1, 7, "Subject", "Body", null, null, CampaignAudienceType.Product, null, 5, 3, Now);

        Assert.Equal(CampaignAudienceType.Product, campaign.AudienceType);
        Assert.Equal(5, campaign.ProductId);
        Assert.Null(campaign.LandingPageId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateQueuedFromTemplate_EmptySubject_Throws(string subject)
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateQueuedFromTemplate(
            1, 7, subject, "Body", null, null, CampaignAudienceType.LandingPage, 10, null, 3, Now));
    }

    [Fact]
    public void CreateQueuedFromTemplate_SubjectTooLong_Throws()
    {
        var subject = new string('a', MailContentRules.MaxSubjectLength + 1);

        Assert.Throws<ArgumentException>(() => Campaign.CreateQueuedFromTemplate(
            1, 7, subject, "Body", null, null, CampaignAudienceType.LandingPage, 10, null, 3, Now));
    }

    [Fact]
    public void CreateQueuedFromTemplate_CtaLabelAndUrlBothSet_Succeeds()
    {
        var campaign = Campaign.CreateQueuedFromTemplate(
            1, 7, "Subject", "Body", "Buy now", "https://example.com", CampaignAudienceType.LandingPage, 10, null, 3, Now);

        Assert.Equal("Buy now", campaign.CtaLabel);
        Assert.Equal("https://example.com", campaign.CtaUrl);
    }

    [Fact]
    public void CreateQueuedFromTemplate_CtaUrlIsJavascriptScheme_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateQueuedFromTemplate(
            1, 7, "Subject", "Body", "Buy now", "javascript:alert(1)", CampaignAudienceType.LandingPage, 10, null, 3, Now));
    }

    [Fact]
    public void CreateQueuedFromTemplate_LandingPageAudienceWithProductIdSet_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateQueuedFromTemplate(
            1, 7, "Subject", "Body", null, null, CampaignAudienceType.LandingPage, 10, 5, 3, Now));
    }

    [Fact]
    public void CreateQueuedFromTemplate_LandingPageAudienceWithoutLandingPageId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateQueuedFromTemplate(
            1, 7, "Subject", "Body", null, null, CampaignAudienceType.LandingPage, null, null, 3, Now));
    }

    [Fact]
    public void CreateQueuedFromTemplate_ProductAudienceWithLandingPageIdSet_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateQueuedFromTemplate(
            1, 7, "Subject", "Body", null, null, CampaignAudienceType.Product, 10, 5, 3, Now));
    }

    [Fact]
    public void CreateQueuedFromTemplate_ZeroRecipientCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateValidLandingPageSend(recipientCount: 0));
    }

    [Fact]
    public void CreateQueuedFromTemplate_NegativeRecipientCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateValidLandingPageSend(recipientCount: -1));
    }
}
