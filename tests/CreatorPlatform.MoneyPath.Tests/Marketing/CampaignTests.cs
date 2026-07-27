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

    private static Campaign CreateValidScheduledSend(DateTimeOffset? scheduledAt = null) => Campaign.CreateScheduled(
        creatorId: 1,
        templateId: 7,
        subject: "Big sale!",
        bodyText: "Check out our new product.",
        ctaLabel: null,
        ctaUrl: null,
        audienceType: CampaignAudienceType.LandingPage,
        landingPageId: 10,
        productId: null,
        scheduledAt: scheduledAt ?? Now.AddMinutes(10),
        createdAt: Now);

    [Fact]
    public void CreateScheduled_ValidLandingPageAudience_CreatesWithZeroRecipientsAndNullQueuedAt()
    {
        var scheduledAt = Now.AddMinutes(10);
        var campaign = CreateValidScheduledSend(scheduledAt);

        Assert.Equal(CampaignStatus.Scheduled, campaign.Status);
        Assert.Equal(0, campaign.RecipientCount);
        Assert.Null(campaign.QueuedAt);
        Assert.Equal(scheduledAt, campaign.ScheduledAt);
        Assert.Null(campaign.Note);
    }

    [Fact]
    public void CreateScheduled_EmptySubject_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateScheduled(
            1, 7, "", "Body", null, null, CampaignAudienceType.LandingPage, 10, null, Now.AddMinutes(10), Now));
    }

    [Fact]
    public void CreateScheduled_ProductAudienceWithLandingPageIdSet_Throws()
    {
        Assert.Throws<ArgumentException>(() => Campaign.CreateScheduled(
            1, 7, "Subject", "Body", null, null, CampaignAudienceType.Product, 10, 5, Now.AddMinutes(10), Now));
    }

    [Fact]
    public void MarkQueuedFromSchedule_FromScheduled_TransitionsToQueuedWithRecipientCount()
    {
        var campaign = CreateValidScheduledSend();
        var queuedAt = Now.AddMinutes(15);

        campaign.MarkQueuedFromSchedule(7, queuedAt);

        Assert.Equal(CampaignStatus.Queued, campaign.Status);
        Assert.Equal(7, campaign.RecipientCount);
        Assert.Equal(queuedAt, campaign.QueuedAt);
    }

    [Fact]
    public void MarkQueuedFromSchedule_ZeroRecipientCount_Throws()
    {
        var campaign = CreateValidScheduledSend();

        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.MarkQueuedFromSchedule(0, Now));
    }

    [Fact]
    public void MarkQueuedFromSchedule_FromQueued_Throws()
    {
        var campaign = CreateValidLandingPageSend();

        Assert.Throws<InvalidOperationException>(() => campaign.MarkQueuedFromSchedule(3, Now));
    }

    [Fact]
    public void MarkFailed_FromScheduled_SetsNoteAndFailedStatus()
    {
        var campaign = CreateValidScheduledSend();
        var failedAt = Now.AddMinutes(15);

        campaign.MarkFailed("No recipients.", failedAt);

        Assert.Equal(CampaignStatus.Failed, campaign.Status);
        Assert.Equal("No recipients.", campaign.Note);
        Assert.Equal(failedAt, campaign.UpdatedAt);
    }

    [Fact]
    public void MarkFailed_FromQueued_Throws()
    {
        var campaign = CreateValidLandingPageSend();

        Assert.Throws<InvalidOperationException>(() => campaign.MarkFailed("oops", Now));
    }

    [Fact]
    public void Cancel_FromScheduled_TransitionsToCancelled()
    {
        var campaign = CreateValidScheduledSend();
        var cancelledAt = Now.AddMinutes(5);

        campaign.Cancel(cancelledAt);

        Assert.Equal(CampaignStatus.Cancelled, campaign.Status);
        Assert.Equal(cancelledAt, campaign.UpdatedAt);
    }

    [Theory]
    [InlineData(CampaignStatus.Queued)]
    [InlineData(CampaignStatus.Failed)]
    [InlineData(CampaignStatus.Cancelled)]
    public void Cancel_FromNonScheduledStatus_Throws(CampaignStatus status)
    {
        var campaign = status switch
        {
            CampaignStatus.Queued => CreateValidLandingPageSend(),
            CampaignStatus.Failed => Failed(),
            CampaignStatus.Cancelled => Cancelled(),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        Assert.Throws<InvalidOperationException>(() => campaign.Cancel(Now));
    }

    private static Campaign Failed()
    {
        var campaign = CreateValidScheduledSend();
        campaign.MarkFailed("boom", Now);
        return campaign;
    }

    private static Campaign Cancelled()
    {
        var campaign = CreateValidScheduledSend();
        campaign.Cancel(Now);
        return campaign;
    }
}
