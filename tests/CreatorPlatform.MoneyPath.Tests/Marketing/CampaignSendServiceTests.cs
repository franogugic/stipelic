using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Infrastructure.Services;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class CampaignSendServiceTests
{
    private const string Slug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;
    private const string LimitKey = "max_email_sends_per_month";
    private static readonly Guid CreatorPublicId = Guid.NewGuid();

    private sealed record Harness(
        CampaignSendService Service,
        FakeMarketingCreatorContextProvider ContextProvider,
        FakeCampaignRepository CampaignRepository,
        FakeCampaignRecipientRepository RecipientRepository,
        FakeAudienceService AudienceService,
        FakeCreatorUsageService UsageService,
        FakeEmailOutboxService EmailOutboxService,
        FakeMarketingUnitOfWork UnitOfWork,
        UnsubscribeTokenService TokenService);

    private static Harness BuildHarness(string? supportEmail = "support@acme.test")
    {
        var context = new MarketingCreatorContext(
            CreatorId, CreatorPublicId, "Acme", Slug, supportEmail, "Acme", null, "#111111", "owner@acme.test");

        var contextProvider = new FakeMarketingCreatorContextProvider
        {
            Context = context,
            PlanLimit = 500,
            LandingPagePublicIds = { [10] = Guid.NewGuid() }
        };
        var campaignRepository = new FakeCampaignRepository();
        var recipientRepository = new FakeCampaignRecipientRepository();
        var audienceService = new FakeAudienceService();
        var usageService = new FakeCreatorUsageService();
        var emailOutboxService = new FakeEmailOutboxService();
        var unitOfWork = new FakeMarketingUnitOfWork();
        var progressProvider = new FakeCampaignProgressProvider();
        var renderer = new CampaignEmailRenderer();
        var tokenService = new UnsubscribeTokenService(
            Options.Create(new MarketingOptions { UnsubscribeTokenSecret = "test-secret-value-1234567890", ApiBaseUrl = "http://localhost:5000" }));

        var service = new CampaignSendService(
            contextProvider,
            campaignRepository,
            recipientRepository,
            audienceService,
            usageService,
            renderer,
            emailOutboxService,
            tokenService,
            progressProvider,
            unitOfWork);

        return new Harness(
            service, contextProvider, campaignRepository, recipientRepository,
            audienceService, usageService, emailOutboxService, unitOfWork, tokenService);
    }

    private static Campaign BuildDraftCampaign(DateTimeOffset now) => Campaign.CreateDraft(
        CreatorId, "Big sale!", "Check it out.", null, null, CampaignAudienceType.LandingPage, 10, null, now);

    [Fact]
    public async Task SendAsync_CreatesExactlyNRecipientsAndNOutboxMessages_WithCorrectPurposeAndCorrelation()
    {
        var h = BuildHarness();
        var campaign = BuildDraftCampaign(DateTimeOffset.UtcNow);
        h.CampaignRepository.Campaigns.Add(campaign);
        h.AudienceService.Emails = ["a@test.com", "b@test.com", "c@test.com"];

        var result = await h.Service.SendAsync(Slug, OwnerUserId, campaign.PublicId, CancellationToken.None);

        Assert.Equal(3, h.RecipientRepository.Recipients.Count);
        Assert.Equal(3, h.EmailOutboxService.QueuedCampaignMessages.Count);
        Assert.Equal("Queued", result.Status);
        Assert.Equal(3, result.RecipientCount);

        foreach (var recipient in h.RecipientRepository.Recipients)
        {
            var expectedKey = $"{campaign.PublicId}:{recipient.Id}";
            Assert.Contains(h.EmailOutboxService.QueuedCampaignMessages, m => m.CorrelationKey == expectedKey && m.ToEmail == recipient.Email);
        }

        Assert.True(h.UnitOfWork.TransactionCommitted);
        Assert.Equal(CreatorId, h.UnitOfWork.LockedCreatorId);
    }

    [Fact]
    public async Task SendAsync_DuplicateEmailInAudience_DedupesToOneRecipient()
    {
        var h = BuildHarness();
        var campaign = BuildDraftCampaign(DateTimeOffset.UtcNow);
        h.CampaignRepository.Campaigns.Add(campaign);
        h.AudienceService.Emails = ["dup@test.com", "dup@test.com", "other@test.com"];

        var result = await h.Service.SendAsync(Slug, OwnerUserId, campaign.PublicId, CancellationToken.None);

        Assert.Equal(2, h.RecipientRepository.Recipients.Count);
        Assert.Equal(2, h.EmailOutboxService.QueuedCampaignMessages.Count);
        Assert.Equal(2, result.RecipientCount);
    }

    [Fact]
    public async Task SendAsync_LimitExceeded_NothingWritten()
    {
        var h = BuildHarness();
        var campaign = BuildDraftCampaign(DateTimeOffset.UtcNow);
        h.CampaignRepository.Campaigns.Add(campaign);
        h.AudienceService.Emails = ["a@test.com", "b@test.com", "c@test.com"];
        h.ContextProvider.PlanLimit = 2;

        await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, campaign.PublicId, CancellationToken.None));

        Assert.Empty(h.RecipientRepository.Recipients);
        Assert.Empty(h.EmailOutboxService.QueuedCampaignMessages);
        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
    }

    [Fact]
    public async Task SendAsync_QueuedCampaign_ThrowsConflict()
    {
        var h = BuildHarness();
        var campaign = BuildDraftCampaign(DateTimeOffset.UtcNow);
        campaign.MarkQueued(5, DateTimeOffset.UtcNow);
        h.CampaignRepository.Campaigns.Add(campaign);

        await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, campaign.PublicId, CancellationToken.None));

        Assert.Empty(h.RecipientRepository.Recipients);
        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SendAsync_EmptyAudience_ThrowsBadRequest()
    {
        var h = BuildHarness();
        var campaign = BuildDraftCampaign(DateTimeOffset.UtcNow);
        h.CampaignRepository.Campaigns.Add(campaign);
        h.AudienceService.Emails = [];

        await Assert.ThrowsAsync<BadRequestException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, campaign.PublicId, CancellationToken.None));

        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SendAsync_NoSupportEmail_FallsBackToOwnerEmail()
    {
        var h = BuildHarness(supportEmail: null);
        var campaign = BuildDraftCampaign(DateTimeOffset.UtcNow);
        h.CampaignRepository.Campaigns.Add(campaign);
        h.AudienceService.Emails = ["a@test.com"];

        await h.Service.SendAsync(Slug, OwnerUserId, campaign.PublicId, CancellationToken.None);

        Assert.Equal("owner@acme.test", h.EmailOutboxService.QueuedCampaignMessages.Single().ReplyTo);
    }

    [Fact]
    public async Task SendAsync_WithSupportEmail_UsesSupportEmailAsReplyTo()
    {
        var h = BuildHarness(supportEmail: "support@acme.test");
        var campaign = BuildDraftCampaign(DateTimeOffset.UtcNow);
        h.CampaignRepository.Campaigns.Add(campaign);
        h.AudienceService.Emails = ["a@test.com"];

        await h.Service.SendAsync(Slug, OwnerUserId, campaign.PublicId, CancellationToken.None);

        Assert.Equal("support@acme.test", h.EmailOutboxService.QueuedCampaignMessages.Single().ReplyTo);
    }

    [Fact]
    public async Task SendAsync_UnsubscribeUrlContainsValidTokenForThatExactRecipient()
    {
        var h = BuildHarness();
        var campaign = BuildDraftCampaign(DateTimeOffset.UtcNow);
        h.CampaignRepository.Campaigns.Add(campaign);
        h.AudienceService.Emails = ["a@test.com", "b@test.com"];

        await h.Service.SendAsync(Slug, OwnerUserId, campaign.PublicId, CancellationToken.None);

        foreach (var message in h.EmailOutboxService.QueuedCampaignMessages)
        {
            var token = message.ListUnsubscribeUrl.Split('/').Last();
            var payload = h.TokenService.TryParse(token);

            Assert.NotNull(payload);
            Assert.Equal(CreatorId, payload!.CreatorId);
            Assert.Equal(message.ToEmail, payload.Email);
        }
    }
}
