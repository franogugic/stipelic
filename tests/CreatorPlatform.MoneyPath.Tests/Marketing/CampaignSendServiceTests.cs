using CreatorPlatform.Email.Domain.Outbox;
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
        UnsubscribeTokenService TokenService,
        FakeCampaignProgressProvider ProgressProvider,
        FakeCampaignLinkRepository LinkRepository);

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
            Options.Create(new MarketingOptions
            {
                UnsubscribeTokenSecret = "test-secret-value-1234567890",
                ApiBaseUrl = "http://localhost:5000",
                FrontendBaseUrl = "https://app.luma.test",
            }));
        var openTrackingTokenService = new OpenTrackingTokenService(
            Options.Create(new MarketingOptions { OpenTrackingSecret = "test-open-secret-value-1234567890", ApiBaseUrl = "http://localhost:5000" }));
        var clickTrackingTokenService = new ClickTrackingTokenService(
            Options.Create(new MarketingOptions { ClickTrackingSecret = "test-click-secret-value-1234567890", ApiBaseUrl = "http://localhost:5000" }));
        var linkRepository = new FakeCampaignLinkRepository();

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
            openTrackingTokenService,
            progressProvider,
            unitOfWork,
            clickTrackingTokenService,
            linkRepository,
            NullLogger<CampaignSendService>.Instance);

        return new Harness(
            service, contextProvider, templateRepository, campaignRepository, recipientRepository,
            audienceService, usageService, emailOutboxService, unitOfWork, tokenService, progressProvider, linkRepository);
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

    [Fact]
    public async Task SendAsync_TheBodyLinksToTheUnsubscribePage_TheHeaderKeepsTheOneClickApiUrl()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];

        await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        var message = Assert.Single(h.EmailOutboxService.QueuedCampaignMessages);
        var token = h.TokenService.Create(CreatorId, "a@test.com");
        Assert.Equal($"http://localhost:5000/api/public/unsubscribe/{token}", message.ListUnsubscribeUrl);
        Assert.Contains($"href=\"https://app.luma.test/unsubscribe/{token}\"", message.HtmlBody);
        Assert.Contains($"https://app.luma.test/unsubscribe/{token}", message.PlainTextBody);
        Assert.DoesNotContain("/api/public/unsubscribe/", message.HtmlBody);
    }

    // --- R2.4: inline content ---

    private static SendCampaignRequestDto BuildInlineRequest(
        string? subject, string? bodyText, string? ctaLabel = null, string? ctaUrl = null, Guid? templatePublicId = null) => new()
    {
        TemplatePublicId = templatePublicId,
        Subject = subject,
        BodyText = bodyText,
        CtaLabel = ctaLabel,
        CtaUrl = ctaUrl,
        AudienceType = "LandingPage",
        TargetPublicId = LandingPagePublicId,
    };

    [Theory]
    [InlineData("Only a subject", null, null, null)]
    [InlineData("Subject", "Body", "Click me", null)]
    [InlineData("Subject", "Body", "Click me", "javascript:alert(1)")]
    public async Task SendAsync_InvalidInlineContent_ThrowsBadRequest_NothingWritten(
        string subject, string? bodyText, string? ctaLabel, string? ctaUrl)
    {
        var h = BuildHarness();
        h.AudienceService.Emails = ["a@test.com"];

        await Assert.ThrowsAsync<BadRequestException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildInlineRequest(subject, bodyText, ctaLabel, ctaUrl), CancellationToken.None));

        Assert.Empty(h.CampaignRepository.Campaigns);
        Assert.Empty(h.UsageService.ConsumeCalls);
        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SendAsync_InlineContentWithTemplate_SnapshotsRequestContentAndKeepsTemplateReference()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];
        typeof(EmailTemplate).GetProperty(nameof(EmailTemplate.Id))!.SetValue(template, 77);

        var result = await h.Service.SendAsync(
            Slug,
            OwnerUserId,
            BuildInlineRequest("Inline subject", "Inline body", "Buy", "https://acme.test/buy", template.PublicId),
            CancellationToken.None);

        Assert.Equal("Inline subject", result.Subject);
        Assert.Equal("Inline body", result.BodyText);
        Assert.Equal("Buy", result.CtaLabel);
        Assert.Equal("https://acme.test/buy", result.CtaUrl);

        var campaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal("Inline subject", campaign.Subject);
        Assert.Equal(77, campaign.TemplateId);
        Assert.NotEqual(template.Subject, campaign.Subject);
    }

    // --- R2.1: All audience ---

    private static SendCampaignRequestDto BuildAllRequest(Guid templatePublicId) => new()
    {
        TemplatePublicId = templatePublicId,
        AudienceType = "All",
    };

    [Fact]
    public async Task SendAsync_AllAudience_ExcludesUnsubscribedAndHasNoDuplicates()
    {
        var h = BuildHarness();
        h.ContextProvider.LandingPageId = null; // proves the All path never resolves a landing page/product
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.AllContactEmails = ["a@test.com", "b@test.com", "b@test.com", "gone@test.com"];
        h.AudienceService.UnsubscribedEmails = ["gone@test.com"];

        var result = await h.Service.SendAsync(Slug, OwnerUserId, BuildAllRequest(template.PublicId), CancellationToken.None);

        Assert.Equal("All", result.AudienceType);
        Assert.Null(result.TargetPublicId);
        Assert.Equal(2, result.RecipientCount);
        Assert.Equal(["a@test.com", "b@test.com"], h.RecipientRepository.Recipients.Select(r => r.Email).Order());
        Assert.Equal(2, h.EmailOutboxService.QueuedCampaignMessages.Count);

        var campaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.Equal(CampaignAudienceType.All, campaign.AudienceType);
        Assert.Null(campaign.LandingPageId);
        Assert.Null(campaign.ProductId);
    }

    [Fact]
    public async Task SendAsync_AllAudienceOverLimit_IsRejectedAndUsageCounterUnchanged()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.AllContactEmails = ["a@test.com", "b@test.com", "c@test.com"];
        h.ContextProvider.PlanLimit = 2;
        h.UsageService.Used[(CreatorId, "max_email_sends_per_month")] = 1;

        await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.SendAsync(Slug, OwnerUserId, BuildAllRequest(template.PublicId), CancellationToken.None));

        Assert.Equal(1, h.UsageService.Used[(CreatorId, "max_email_sends_per_month")]);
        Assert.Empty(h.CampaignRepository.Campaigns);
        Assert.Empty(h.RecipientRepository.Recipients);
        Assert.Empty(h.EmailOutboxService.QueuedCampaignMessages);
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

    // --- Task 04.5: manual resend of failed recipients ---

    [Fact]
    public async Task ResendFailedAsync_NoFailedRecipients_IsNoOpWithZeroCount()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);
        // ProgressProvider.FailedCountByCampaign left empty -> 0 failed for this campaign.

        var result = await h.Service.ResendFailedAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None);

        Assert.Equal(0, result.RequeuedCount);
    }

    [Fact]
    public async Task ResendFailedAsync_HasFailedRecipients_RequeuesOnlyThisCampaignAndReturnsCount()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com", "b@test.com"];
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        var otherCampaignId = Guid.NewGuid();
        h.ProgressProvider.FailedCountByCampaign[sent.PublicId] = 2;
        h.ProgressProvider.FailedCountByCampaign[otherCampaignId] = 5; // a different campaign's failures

        var result = await h.Service.ResendFailedAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None);

        Assert.Equal(2, result.RequeuedCount);
        var call = Assert.Single(h.ProgressProvider.RequeueFailedCalls);
        Assert.Equal(sent.PublicId, call.CampaignPublicId); // never the other campaign's id
    }

    [Fact]
    public async Task ResendFailedAsync_ConsumesOneSendPerRequeuedMessage_InTheCampaignsPeriod_UnderTheCreatorLock()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com", "b@test.com", "c@test.com"];
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);
        var campaign = h.CampaignRepository.Campaigns.Single();
        h.UsageService.ConsumeCalls.Clear();
        h.UsageService.ConsumeAsOf.Clear();
        h.ProgressProvider.FailedCountByCampaign[sent.PublicId] = 2;

        var result = await h.Service.ResendFailedAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None);

        Assert.Equal(2, result.RequeuedCount);
        var call = Assert.Single(h.UsageService.ConsumeCalls);
        Assert.Equal((CreatorId, "max_email_sends_per_month", 2, 500), (call.CreatorId, call.UsageKey, call.Amount, call.Limit));
        Assert.Equal(campaign.QueuedAt, Assert.Single(h.UsageService.ConsumeAsOf));
        Assert.Equal(CreatorId, h.UnitOfWork.LockedCreatorId);
        Assert.Equal(5, h.UsageService.Used[(CreatorId, "max_email_sends_per_month")]);
    }

    [Fact]
    public async Task ResendFailedAsync_OverTheMonthlyLimit_Is409WithTheRemainingCount_AndSavesNothing()
    {
        var h = BuildHarness();
        h.ContextProvider.PlanLimit = 3;
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com", "b@test.com"];
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);
        h.ProgressProvider.FailedCountByCampaign[sent.PublicId] = 2; // 2 used + 2 resent > 3
        var savesBefore = h.UnitOfWork.SaveChangesCallCount;

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.ResendFailedAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None));

        Assert.Equal(CampaignSendService.EmailLimitReachedCode, exception.Code);
        Assert.Equal(new CampaignSendService.EmailLimitDetails(Remaining: 1), exception.Details);
        Assert.Equal(savesBefore, h.UnitOfWork.SaveChangesCallCount); // the in-memory requeue is never written
        Assert.Equal(2, h.UsageService.Used[(CreatorId, "max_email_sends_per_month")]);
    }

    [Fact]
    public async Task ResendThenFailAgain_IsRefundedAgain_SoTheCounterEqualsTheDeliveredCount()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com", "b@test.com", "c@test.com"];
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);
        var failures = new CampaignBroadcastFailureHandler(
            h.CampaignRepository, h.UsageService, NullLogger<CampaignBroadcastFailureHandler>.Instance);
        var failed = EmailOutboxMessage.Create(
            EmailOutboxMessagePurpose.CampaignBroadcast, $"{sent.PublicId}:1", "a@test.com", "s", "h", "t", DateTimeOffset.UtcNow);

        // 3 sent, 1 failed → refunded: 2 delivered.
        await failures.HandleAsync(failed, CancellationToken.None);
        // Resent → consumed again, fails again → refunded again: still 2 delivered.
        h.ProgressProvider.FailedCountByCampaign[sent.PublicId] = 1;
        await h.Service.ResendFailedAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None);
        Assert.Equal(3, h.UsageService.Used[(CreatorId, "max_email_sends_per_month")]);
        await failures.HandleAsync(failed, CancellationToken.None);

        Assert.Equal(2, h.UsageService.Used[(CreatorId, "max_email_sends_per_month")]);
    }

    [Fact]
    public async Task ResendFailedAsync_NothingFailed_ConsumesNothing()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);
        h.UsageService.ConsumeCalls.Clear();

        await h.Service.ResendFailedAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None);

        Assert.Empty(h.UsageService.ConsumeCalls);
    }

    [Fact]
    public async Task ResendFailedAsync_UnknownCampaign_ThrowsNotFound()
    {
        var h = BuildHarness();

        await Assert.ThrowsAsync<NotFoundException>(
            () => h.Service.ResendFailedAsync(Slug, OwnerUserId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ResendFailedAsync_CampaignOwnedByAnotherCreator_ThrowsNotFound()
    {
        var h = BuildHarness();
        var template = BuildActiveTemplate(DateTimeOffset.UtcNow);
        h.TemplateRepository.Templates.Add(template);
        h.AudienceService.Emails = ["a@test.com"];
        var sent = await h.Service.SendAsync(Slug, OwnerUserId, BuildRequest(template.PublicId), CancellationToken.None);

        // Simulate a different creator's workspace resolving to a different CreatorId.
        h.ContextProvider.Context = h.ContextProvider.Context! with { CreatorId = 999 };

        await Assert.ThrowsAsync<NotFoundException>(
            () => h.Service.ResendFailedAsync(Slug, OwnerUserId, sent.PublicId, CancellationToken.None));
    }

    private static SendCampaignRequestDto BuildScheduledRequest(Guid templatePublicId, DateTimeOffset scheduledAt) => new()
    {
        TemplatePublicId = templatePublicId,
        AudienceType = "LandingPage",
        TargetPublicId = LandingPagePublicId,
        ScheduledAt = scheduledAt,
    };

    [Fact]
    public async Task SendAsync_CreatesTheLinksOnce_AndGivesEachRecipientItsOwnTrackedUrls()
    {
        var h = BuildHarness();
        h.AudienceService.Emails = ["a@test.com", "b@test.com"];

        await h.Service.SendAsync(
            Slug, OwnerUserId,
            BuildInlineRequest("Hi", "Read https://blog.test/post.", "Buy", "https://shop.test/buy"),
            CancellationToken.None);

        var campaign = Assert.Single(h.CampaignRepository.Campaigns);
        Assert.True(campaign.ClickTracked);
        Assert.Equal(
            [(1, "https://blog.test/post", CampaignLinkKind.Text, (string?)null), (2, "https://shop.test/buy", CampaignLinkKind.Button, "Buy")],
            h.LinkRepository.Links.Select(l => ((int)l.Position, l.Url, l.Kind, l.Label)));
        Assert.All(h.LinkRepository.Links, l => Assert.Equal(campaign.Id, l.CampaignId));

        var messages = h.EmailOutboxService.QueuedCampaignMessages;
        Assert.Equal(2, messages.Count);
        var trackedUrls = messages.Select(m => System.Text.RegularExpressions.Regex.Matches(m.HtmlBody, "http://localhost:5000/api/public/c/[A-Za-z0-9_.-]+")
            .Select(x => x.Value).Distinct().ToList()).ToList();
        Assert.All(trackedUrls, urls => Assert.Equal(2, urls.Count));
        Assert.Empty(trackedUrls[0].Intersect(trackedUrls[1]));
        foreach (var (message, urls) in messages.Zip(trackedUrls))
        {
            Assert.DoesNotContain("href=\"https://blog.test", message.HtmlBody);
            Assert.DoesNotContain("href=\"https://shop.test", message.HtmlBody);
            Assert.DoesNotContain("{{LINK_", message.HtmlBody);
            Assert.All(urls, url => Assert.Contains(url, message.PlainTextBody));
            Assert.DoesNotContain("https://blog.test", message.PlainTextBody);
            Assert.Contains("/unsubscribe/", message.PlainTextBody);
            Assert.DoesNotContain("/api/public/c/", message.ListUnsubscribeUrl);
        }
    }

    [Fact]
    public async Task SendAsync_WithoutLinks_IsStillClickTracked_WithNoLinks()
    {
        var h = BuildHarness();
        h.AudienceService.Emails = ["a@test.com"];

        await h.Service.SendAsync(Slug, OwnerUserId, BuildInlineRequest("Hi", "Just text."), CancellationToken.None);

        Assert.True(Assert.Single(h.CampaignRepository.Campaigns).ClickTracked);
        Assert.Empty(h.LinkRepository.Links);
    }
}
