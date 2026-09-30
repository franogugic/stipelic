using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Infrastructure.Services;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class OpenTrackingServiceTests
{
    private static readonly MarketingOptions Options = new()
    {
        OpenTrackingSecret = "test-open-secret-value-1234567890",
        UnsubscribeTokenSecret = "test-secret-value-1234567890",
        ApiBaseUrl = "http://localhost:5000",
    };

    private sealed record Harness(
        OpenTrackingService Service,
        OpenTrackingTokenService TokenService,
        FakeCampaignRepository CampaignRepository,
        FakeCampaignRecipientRepository RecipientRepository,
        Campaign Campaign,
        CampaignRecipient Recipient);

    private static async Task<Harness> BuildHarnessAsync()
    {
        var campaignRepository = new FakeCampaignRepository();
        var recipientRepository = new FakeCampaignRecipientRepository(campaignRepository);
        var tokenService = new OpenTrackingTokenService(Microsoft.Extensions.Options.Options.Create(Options));

        var now = DateTimeOffset.UtcNow;
        var campaign = Campaign.CreateQueued(
            1, null, "Subject", "Body", null, null, CampaignAudienceType.All, null, null, 1, now);
        await campaignRepository.AddAsync(campaign, CancellationToken.None);

        var recipient = CampaignRecipient.Create(campaign.Id, "a@test.com", now);
        await recipientRepository.AddRangeAsync([recipient], CancellationToken.None);

        var service = new OpenTrackingService(tokenService, recipientRepository, NullLogger<OpenTrackingService>.Instance);

        return new Harness(service, tokenService, campaignRepository, recipientRepository, campaign, recipient);
    }

    [Fact]
    public async Task RecordOpenAsync_CalledTwiceForSameRecipient_CountsOneUniqueOpen()
    {
        var h = await BuildHarnessAsync();
        var token = h.TokenService.Create(h.Recipient.Id);

        await h.Service.RecordOpenAsync(token, CancellationToken.None);
        await h.Service.RecordOpenAsync(token, CancellationToken.None);

        Assert.Equal(1, h.Campaign.UniqueOpenCount);
        Assert.NotNull(h.Recipient.FirstOpenedAt);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("MQ.AAAA")]
    public async Task RecordOpenAsync_InvalidToken_ChangesNothing(string token)
    {
        var h = await BuildHarnessAsync();

        await h.Service.RecordOpenAsync(token, CancellationToken.None);

        Assert.Equal(0, h.Campaign.UniqueOpenCount);
        Assert.Null(h.Recipient.FirstOpenedAt);
    }
}
