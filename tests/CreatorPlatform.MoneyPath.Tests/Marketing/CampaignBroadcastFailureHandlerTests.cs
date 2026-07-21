using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class CampaignBroadcastFailureHandlerTests
{
    private const int CreatorId = 7;
    private const string MonthlyEmailSendsLimitKey = "max_email_sends_per_month";

    private static (CampaignBroadcastFailureHandler Handler, FakeCampaignRepository CampaignRepository, FakeCreatorUsageService UsageService) BuildHandler()
    {
        var campaignRepository = new FakeCampaignRepository();
        var usageService = new FakeCreatorUsageService();
        var handler = new CampaignBroadcastFailureHandler(
            campaignRepository, usageService, NullLogger<CampaignBroadcastFailureHandler>.Instance);

        return (handler, campaignRepository, usageService);
    }

    private static EmailOutboxMessage BuildMessage(string correlationKey, DateTimeOffset now) =>
        EmailOutboxMessage.Create(
            EmailOutboxMessagePurpose.CampaignBroadcast,
            correlationKey,
            "recipient@test.com",
            "Subject",
            "<p>Body</p>",
            "Body",
            now);

    [Fact]
    public async Task HandleAsync_KnownCampaign_RefundsOneUnitForThePeriodItWasQueuedIn()
    {
        var (handler, campaignRepository, usageService) = BuildHandler();
        var queuedAt = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var campaign = Campaign.CreateQueuedFromTemplate(
            CreatorId, 1, "Subject", "Body", null, null,
            CampaignAudienceType.LandingPage, 10, null, 1, queuedAt);
        await campaignRepository.AddAsync(campaign, CancellationToken.None);

        usageService.Used[(CreatorId, MonthlyEmailSendsLimitKey)] = 5;

        var message = BuildMessage($"{campaign.PublicId}:1", DateTimeOffset.UtcNow);

        await handler.HandleAsync(message, CancellationToken.None);

        var refund = Assert.Single(usageService.RefundCalls);
        Assert.Equal(CreatorId, refund.CreatorId);
        Assert.Equal(MonthlyEmailSendsLimitKey, refund.UsageKey);
        Assert.Equal(1, refund.Amount);
        Assert.Equal(UsagePeriod.CalendarMonth, refund.Period);
        Assert.Equal(queuedAt, refund.AsOf);
        Assert.Equal(4, usageService.Used[(CreatorId, MonthlyEmailSendsLimitKey)]);
    }

    [Fact]
    public async Task HandleAsync_RefundFloorsAtZero_NeverGoesNegative()
    {
        var (handler, campaignRepository, usageService) = BuildHandler();
        var queuedAt = DateTimeOffset.UtcNow;

        var campaign = Campaign.CreateQueuedFromTemplate(
            CreatorId, 1, "Subject", "Body", null, null,
            CampaignAudienceType.LandingPage, 10, null, 1, queuedAt);
        await campaignRepository.AddAsync(campaign, CancellationToken.None);

        // No prior consumption recorded for this creator/key — refunding must not go negative.
        var message = BuildMessage($"{campaign.PublicId}:1", DateTimeOffset.UtcNow);

        await handler.HandleAsync(message, CancellationToken.None);

        Assert.Single(usageService.RefundCalls);
        Assert.Equal(0, usageService.Used[(CreatorId, MonthlyEmailSendsLimitKey)]);
    }

    [Fact]
    public async Task HandleAsync_UnparseableCorrelationKey_DoesNotThrowAndDoesNotRefund()
    {
        var (handler, _, usageService) = BuildHandler();
        var message = BuildMessage("not-a-guid:1", DateTimeOffset.UtcNow);

        await handler.HandleAsync(message, CancellationToken.None);

        Assert.Empty(usageService.RefundCalls);
    }

    [Fact]
    public async Task HandleAsync_UnknownCampaign_DoesNotThrowAndDoesNotRefund()
    {
        var (handler, _, usageService) = BuildHandler();
        var message = BuildMessage($"{Guid.NewGuid()}:1", DateTimeOffset.UtcNow);

        await handler.HandleAsync(message, CancellationToken.None);

        Assert.Empty(usageService.RefundCalls);
    }

    [Fact]
    public void Purpose_IsCampaignBroadcast()
    {
        var (handler, _, _) = BuildHandler();
        Assert.Equal(EmailOutboxMessagePurpose.CampaignBroadcast, handler.Purpose);
    }
}
