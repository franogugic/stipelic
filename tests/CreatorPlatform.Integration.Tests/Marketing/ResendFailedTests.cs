using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Marketing.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Marketing;

/// <summary>Resending a campaign's failed emails uses the monthly sends again, under the creator's lock.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ResendFailedTests
{
    private const string MonthlySendsKey = "max_email_sends_per_month";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public ResendFailedTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<T> WithServiceAsync<T>(Func<CampaignSendService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        var marketing = new MarketingOptions
        {
            UnsubscribeTokenSecret = "test-secret-value-1234567890",
            OpenTrackingSecret = "test-open-secret-value-1234567890",
            ApiBaseUrl = "http://localhost:5000",
            FrontendBaseUrl = "https://app.luma.test",
        };
        return await act(new CampaignSendService(
            new MarketingCreatorContextProvider(db),
            new EmailTemplateRepository(db),
            new CampaignRepository(db),
            new CampaignRecipientRepository(db),
            new AudienceService(db),
            new CreatorUsageService(db),
            new CampaignEmailRenderer(),
            new EmailOutboxService(db, Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.luma.test" })),
            new UnsubscribeTokenService(Options.Create(marketing)),
            new OpenTrackingTokenService(Options.Create(marketing)),
            new CampaignProgressProvider(db),
            new MarketingUnitOfWork(db),
            NullLogger<CampaignSendService>.Instance));
    }

    /// <summary>A sent campaign whose <paramref name="failed"/> emails failed for good (and were refunded), with
    /// <paramref name="used"/> sends on this month's counter.</summary>
    private async Task<Campaign> SeedAsync(int creatorId, int failed, int used)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var campaign = Campaign.CreateQueued(creatorId, null, "Hello", "Body", null, null, CampaignAudienceType.All, null, null, failed, now);
        db.Add(campaign);
        for (var i = 0; i < failed; i++)
        {
            var message = EmailOutboxMessage.Create(
                EmailOutboxMessagePurpose.CampaignBroadcast, $"{campaign.PublicId}:{i}", $"r{i}@example.test", "Hello", "<p>Body</p>", "Body", now);
            message.MarkAsFailed("bounced", now, maxRetryCount: 1);
            db.Add(message);
        }
        await db.SaveChangesAsync();
        await new CreatorUsageService(db).TryConsumeAsync(creatorId, MonthlySendsKey, used, -1, UsagePeriod.CalendarMonth, CancellationToken.None);
        return campaign;
    }

    private async Task<(int Used, int Failed, int Pending)> StateAsync(int creatorId, Guid campaignPublicId)
    {
        await using var db = _fixture.CreateDbContext();
        var used = await new CreatorUsageService(db).GetUsedAsync(creatorId, MonthlySendsKey, UsagePeriod.CalendarMonth, CancellationToken.None);
        var messages = await db.Set<EmailOutboxMessage>().AsNoTracking()
            .Where(m => m.CorrelationKey.StartsWith(campaignPublicId.ToString()))
            .Select(m => m.Status)
            .ToListAsync();
        return (used, messages.Count(s => s == EmailOutboxMessageStatus.Failed), messages.Count(s => s == EmailOutboxMessageStatus.Pending));
    }

    [Fact]
    public async Task ConcurrentResends_RequeueAndConsumeEachFailedEmailOnce()
    {
        var creator = await _data.CreateCreatorAsync();
        var campaign = await SeedAsync(creator.CreatorId, failed: 3, used: 10);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            WithServiceAsync(s => s.ResendFailedAsync(creator.Slug, creator.OwnerUserId, campaign.PublicId, CancellationToken.None))));

        Assert.Equal(3, results.Sum(r => r.RequeuedCount));
        Assert.Equal((13, 0, 3), await StateAsync(creator.CreatorId, campaign.PublicId));
    }

    [Fact]
    public async Task ResendOverTheLimit_Is409_AndRequeuesNothing()
    {
        var creator = await _data.CreateCreatorAsync();
        var limit = await PlanLimitAsync(creator.CreatorId);
        var campaign = await SeedAsync(creator.CreatorId, failed: 3, used: limit - 2);

        var exception = await Assert.ThrowsAsync<CreatorPlatform.Shared.Application.Exceptions.ConflictException>(() =>
            WithServiceAsync(s => s.ResendFailedAsync(creator.Slug, creator.OwnerUserId, campaign.PublicId, CancellationToken.None)));

        Assert.Equal(CampaignSendService.EmailLimitReachedCode, exception.Code);
        Assert.Equal(new CampaignSendService.EmailLimitDetails(2), exception.Details);
        Assert.Equal((limit - 2, 3, 0), await StateAsync(creator.CreatorId, campaign.PublicId));
    }

    private async Task<int> PlanLimitAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await new MarketingCreatorContextProvider(db).GetActivePlanLimitAsync(creatorId, MonthlySendsKey, CancellationToken.None))!.Value;
    }
}
