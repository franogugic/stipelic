using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Templates;
using CreatorPlatform.Marketing.Infrastructure.Services;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class CampaignSendServiceTests
{
    private const string Slug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;
    private static readonly Guid CreatorPublicId = Guid.NewGuid();
    private static readonly Guid LandingPagePublicId = Guid.NewGuid();

    private sealed record Harness(
        CampaignSendService Service,
        FakeMarketingCreatorContextProvider ContextProvider,
        FakeEmailTemplateRepository TemplateRepository,
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
            LandingPageId = 10,
            LandingPagePublicIds = { [10] = LandingPagePublicId }
        };
        var templateRepository = new FakeEmailTemplateRepository();
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
            templateRepository,
            campaignRepository,
            recipientRepository,
            audienceService,
            usageService,
            renderer,
            emailOutboxService,
            tokenService,
            progressProvider,
            unitOfWork,
            NullLogger<CampaignSendService>.Instance);

        return new Harness(
            service, contextProvider, templateRepository, campaignRepository, recipientRepository,
            audienceService, usageService, emailOutboxService, unitOfWork, tokenService);
    }

    private static EmailTemplate BuildActiveTemplate(DateTimeOffset now) => EmailTemplate.Create(
        CreatorId, "Summer sale", "Big sale!", "Check it out.", null, null, now);

    private static SendCampaignRequestDto BuildRequest(Guid templatePublicId) => new()
    {
        TemplatePublicId = templatePublicId,
        AudienceType = "LandingPage",
        TargetPublicId = LandingPagePublicId,
    };

    [Fact]
    public async Task SendAsync_CreatesExactlyNRecipientsAndNOutboxMessages_WithCorrectPurposeAndCorrelation()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com", "b@test.com", "c@test.com"];

        var result = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        Assert.Equal(3, h.RecipientRepository.Recipients.Count);
        Assert.Equal(3, h.EmailOutboxService.QueuedCampaignMessages.Count);
        Assert.Equal("Queued", result.Status);
        Assert.Equal(3, result.RecipientCount);

        var sentCampaign = Assert.Single(h.CampaignRepository.Campaigns);
        foreach (var recipient in h.RecipientRepository.Recipients)
        {
            var expectedKey = $"{sentCampaign.PublicId}:{recipient.Id}";
            Assert.Contains(h.EmailOutboxService.QueuedCampaignMessages, m => m.CorrelationKey == expectedKey && m.ToEmail == recipient.Email);
        }

        Assert.True(h.UnitOfWork.TransactionCommitted);
        Assert.Equal(CreatorId, h.UnitOfWork.LockedCreatorId);
    }

    [Fact]
    public async Task SendAsync_SnapshotsTemplateContent_LaterTemplateEditDoesNotChangeTheSend()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];

        var result = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);
        Assert.Equal("Big sale!", result.Subject);

        template.Update("Renamed", "Changed subject", "Changed body", null, null, DateTimeOffset.UtcNow);

        var sentCampaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal("Big sale!", sentCampaign.Subject);
        Assert.Equal("Check it out.", sentCampaign.BodyText);
    }

    [Fact]
    public async Task SendAsync_ArchivedTemplate_ThrowsConflict()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        template.Archive(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];

        await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None));

        Assert.Empty(h.CampaignRepository.Campaigns);
        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SendAsync_TemplateOwnedByAnotherCreator_ThrowsNotFound()
    {
        var h = BuildHarness();
        var foreignTemplate = EmailTemplate.Create(999, "Not yours", "Subject", "Body", null, null, DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(foreignTemplate);
        h.AudienceService.Emails = ["a@test.com"];

        await Assert.ThrowsAsync<NotFoundException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(foreignTemplate.PublicId), CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_UnknownTemplate_ThrowsNotFound()
    {
        var h = BuildHarness();
        h.AudienceService.Emails = ["a@test.com"];

        await Assert.ThrowsAsync<NotFoundException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_DuplicateEmailInAudience_DedupesToOneRecipient()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["dup@test.com", "dup@test.com", "other@test.com"];

        var result = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        Assert.Equal(2, h.RecipientRepository.Recipients.Count);
        Assert.Equal(2, h.EmailOutboxService.QueuedCampaignMessages.Count);
        Assert.Equal(2, result.RecipientCount);
    }

    [Fact]
    public async Task SendAsync_LimitExceeded_NothingWritten()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com", "b@test.com", "c@test.com"];
        h.ContextProvider.PlanLimit = 2;

        await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None));

        Assert.Empty(h.RecipientRepository.Recipients);
        Assert.Empty(h.EmailOutboxService.QueuedCampaignMessages);
        Assert.Empty(h.CampaignRepository.Campaigns);
        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SendAsync_EmptyAudience_ThrowsBadRequest()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = [];

        await Assert.ThrowsAsync<BadRequestException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None));

        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SendAsync_NoSupportEmail_FallsBackToOwnerEmail()
    {
        var h = BuildHarness(supportEmail: null);
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];

        await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        Assert.Equal("owner@acme.test", h.EmailOutboxService.QueuedCampaignMessages.Single().ReplyTo);
    }

    [Fact]
    public async Task SendAsync_WithSupportEmail_UsesSupportEmailAsReplyTo()
    {
        var h = BuildHarness(supportEmail: "support@acme.test");
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];

        await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        Assert.Equal("support@acme.test", h.EmailOutboxService.QueuedCampaignMessages.Single().ReplyTo);
    }

    [Fact]
    public async Task SendAsync_UnsubscribeUrlContainsValidTokenForThatExactRecipient()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com", "b@test.com"];

        await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        foreach (var message in h.EmailOutboxService.QueuedCampaignMessages)
        {
            var token = message.ListUnsubscribeUrl.Split('/').Last();
            var payload = h.TokenService.TryParse(token);

            Assert.NotNull(payload);
            Assert.Equal(CreatorId, payload!.CreatorId);
            Assert.Equal(message.ToEmail, payload.Email);
        }
    }

    // --- Task 14: scheduled sends ---

    [Fact]
    public async Task SendAsync_ScheduledAtFuture_CreatesScheduledCampaign_TouchesNoAudienceLimitOrOutbox()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com", "b@test.com"];
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);

        var result = await h.Service.SendAsync(
            Slug, OwnerUserId, BuildScheduledRequest(template.PublicId, scheduledAt), CancellationToken.None);

        Assert.Equal("Scheduled", result.Status);
        Assert.Equal(0, result.RecipientCount);
        Assert.Equal(scheduledAt, result.ScheduledAt);
        Assert.Null(result.QueuedAt);

        Assert.Equal(0, h.AudienceService.GetAudienceEmailsCallCount);
        Assert.Empty(h.UsageService.ConsumeCalls);
        Assert.Empty(h.RecipientRepository.Recipients);
        Assert.Empty(h.EmailOutboxService.QueuedCampaignMessages);

        var sentCampaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal(CampaignStatus.Scheduled, sentCampaign.Status);
    }

    [Theory]
    [InlineData(-10)] // in the past
    [InlineData(1)]   // under the 2-minute buffer
    public async Task SendAsync_ScheduledAtTooSoon_ThrowsBadRequest_NothingWritten(int minutesFromNow)
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(minutesFromNow);

        await Assert.ThrowsAsync<BadRequestException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildScheduledRequest(template.PublicId, scheduledAt), CancellationToken.None));

        Assert.Empty(h.CampaignRepository.Campaigns);
        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SendAsync_ScheduledAtTooFar_ThrowsBadRequest_NothingWritten()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        var scheduledAt = DateTimeOffset.UtcNow.AddDays(400);

        await Assert.ThrowsAsync<BadRequestException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildScheduledRequest(template.PublicId, scheduledAt), CancellationToken.None));

        Assert.Empty(h.CampaignRepository.Campaigns);
        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DispatchScheduledAsync_HappyPath_ParityWithImmediateSend()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildScheduledRequest(template.PublicId, scheduledAt), CancellationToken.None);

        h.AudienceService.Emails = ["a@test.com", "b@test.com", "c@test.com"];

        await h.Service.DispatchScheduledAsync(sent.PublicId, CancellationToken.None);

        var campaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal(CampaignStatus.Queued, campaign.Status);
        Assert.Equal(3, campaign.RecipientCount);
        Assert.NotNull(campaign.QueuedAt);

        Assert.Equal(3, h.RecipientRepository.Recipients.Count);
        Assert.Equal(3, h.EmailOutboxService.QueuedCampaignMessages.Count);
        foreach (var recipient in h.RecipientRepository.Recipients)
        {
            var expectedKey = $"{campaign.PublicId}:{recipient.Id}";
            Assert.Contains(h.EmailOutboxService.QueuedCampaignMessages, m => m.CorrelationKey == expectedKey && m.ToEmail == recipient.Email);
        }
    }

    [Fact]
    public async Task DispatchScheduledAsync_AudienceEmpty_MarksFailedWithNote_NoPartialRecipientsOrOutbox()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildScheduledRequest(template.PublicId, scheduledAt), CancellationToken.None);

        h.AudienceService.Emails = [];

        await h.Service.DispatchScheduledAsync(sent.PublicId, CancellationToken.None);

        var campaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal(CampaignStatus.Failed, campaign.Status);
        Assert.Equal("No recipients.", campaign.Note);
        Assert.Empty(h.RecipientRepository.Recipients);
        Assert.Empty(h.EmailOutboxService.QueuedCampaignMessages);
    }

    [Fact]
    public async Task DispatchScheduledAsync_LimitExceeded_MarksFailedWithNote_NoPartialRecipientsOrOutbox()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildScheduledRequest(template.PublicId, scheduledAt), CancellationToken.None);

        h.AudienceService.Emails = ["a@test.com", "b@test.com", "c@test.com"];
        h.ContextProvider.PlanLimit = 1;

        await h.Service.DispatchScheduledAsync(sent.PublicId, CancellationToken.None);

        var campaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal(CampaignStatus.Failed, campaign.Status);
        Assert.NotNull(campaign.Note);
        Assert.Empty(h.RecipientRepository.Recipients);
        Assert.Empty(h.EmailOutboxService.QueuedCampaignMessages);
    }

    [Fact]
    public async Task DispatchScheduledAsync_CalledTwiceInARow_SecondCallIsNoOp()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildScheduledRequest(template.PublicId, scheduledAt), CancellationToken.None);

        h.AudienceService.Emails = ["a@test.com", "b@test.com"];

        await h.Service.DispatchScheduledAsync(sent.PublicId, CancellationToken.None);
        await h.Service.DispatchScheduledAsync(sent.PublicId, CancellationToken.None);

        Assert.Equal(2, h.RecipientRepository.Recipients.Count);
        Assert.Equal(2, h.EmailOutboxService.QueuedCampaignMessages.Count);
        var campaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal(CampaignStatus.Queued, campaign.Status);
    }

    [Fact]
    public async Task DispatchScheduledAsync_UnknownCampaign_DoesNotThrow()
    {
        var h = BuildHarness();

        await h.Service.DispatchScheduledAsync(Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task CancelScheduledAsync_FromScheduled_TransitionsToCancelled()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(10);
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildScheduledRequest(template.PublicId, scheduledAt), CancellationToken.None);

        var result = await h.Service.CancelScheduledAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None);

        Assert.Equal("Cancelled", result.Status);
        var campaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal(CampaignStatus.Cancelled, campaign.Status);
    }

    [Fact]
    public async Task CancelScheduledAsync_AlreadyQueued_ThrowsConflict()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.CancelScheduledAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None));
    }

    [Fact]
    public async Task CancelScheduledAsync_UnknownCampaign_ThrowsNotFound()
    {
        var h = BuildHarness();

        await Assert.ThrowsAsync<NotFoundException>(
            () => h.Service.CancelScheduledAsync(Slug, OwnerUserId, Guid.NewGuid(), CancellationToken.None));
    }

    private static SendCampaignRequestDto BuildScheduledRequest(Guid templatePublicId, DateTimeOffset scheduledAt) => new()
    {
        TemplatePublicId = templatePublicId,
        AudienceType = "LandingPage",
        TargetPublicId = LandingPagePublicId,
        ScheduledAt = scheduledAt,
    };
}
