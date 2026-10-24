using System.Text.RegularExpressions;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Dtos;
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

/// <summary>
/// Click tracking (F2.1) on the real send path and database: each recipient's email carries its own tracked URLs,
/// a click redirects to the URL stored for the link, and the counters stay right for repeated, parallel, multi-link
/// and multi-recipient clicks.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ClickTrackingTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static readonly MarketingOptions Marketing = new()
    {
        UnsubscribeTokenSecret = "test-secret-value-1234567890",
        OpenTrackingSecret = "test-open-secret-value-1234567890",
        ClickTrackingSecret = "test-click-secret-value-1234567890",
        ApiBaseUrl = "http://localhost:5000",
        FrontendBaseUrl = "https://app.luma.test",
    };

    private static readonly Regex TrackedUrl = new(@"http://localhost:5000/api/public/c/(?<token>[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+)");

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public ClickTrackingTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    // ---- harness ----------------------------------------------------------------------------------------------

    private async Task<T> SendServiceAsync<T>(Func<CampaignSendService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(new CampaignSendService(
            new MarketingCreatorContextProvider(db),
            new EmailTemplateRepository(db),
            new CampaignRepository(db),
            new CampaignRecipientRepository(db),
            new AudienceService(db),
            new CreatorUsageService(db),
            new CampaignEmailRenderer(),
            new EmailOutboxService(db, Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.luma.test" })),
            new UnsubscribeTokenService(Options.Create(Marketing)),
            new OpenTrackingTokenService(Options.Create(Marketing)),
            new CampaignProgressProvider(db),
            new MarketingUnitOfWork(db),
            new ClickTrackingTokenService(Options.Create(Marketing)),
            new CampaignLinkRepository(db),
            NullLogger<CampaignSendService>.Instance));
    }

    private async Task<T> ReadServiceAsync<T>(Func<CampaignService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(new CampaignService(
            new MarketingCreatorContextProvider(db),
            new AudienceService(db),
            new CreatorUsageService(db),
            new CampaignRepository(db),
            new CampaignProgressProvider(db),
            new CampaignLinkRepository(db)));
    }

    private async Task<string?> ClickAsync(string token)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new ClickTrackingService(
            new ClickTrackingTokenService(Options.Create(Marketing)), new CampaignLinkRepository(db), NullLogger<ClickTrackingService>.Instance);
        return await service.ResolveClickAsync(token, None);
    }

    /// <summary>A sent campaign to every given contact (All audience), body with a text link and a CTA button.</summary>
    private async Task<(TestData.SeededCreator Creator, CampaignDetailDto Campaign)> SendAsync(
        params string[] emails)
    {
        var creator = await _data.CreateCreatorAsync();
        await AddContactsAsync(creator.CreatorId, emails);

        var campaign = await SendServiceAsync(s => s.SendAsync(creator.Slug, creator.OwnerUserId, new SendCampaignRequestDto
        {
            Subject = "New post",
            BodyText = "Read it: https://blog.test/post.\nOr the archive (https://blog.test/archive).",
            CtaLabel = "Shop now",
            CtaUrl = "https://shop.test/buy",
            AudienceType = "All"
        }, None));

        return (creator, campaign);
    }

    private async Task AddContactsAsync(int creatorId, IEnumerable<string> emails)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        foreach (var email in emails)
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO marketing.contact_summaries ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds")
                VALUES ({creatorId}, {email}, {now}, {now}, {Array.Empty<int>()})
                """);
    }

    /// <summary>Each recipient's tracked-link tokens from their queued email, keyed by destination.</summary>
    private async Task<Dictionary<string, Dictionary<string, string>>> TokensByRecipientAsync(Guid campaignPublicId)
    {
        await using var db = _fixture.CreateDbContext();
        var prefix = campaignPublicId + ":";
        var messages = await db.Set<EmailOutboxMessage>().AsNoTracking()
            .Where(m => m.CorrelationKey.StartsWith(prefix))
            .Select(m => new { m.ToEmail, m.HtmlBody, m.PlainTextBody })
            .ToListAsync();
        var links = await db.Set<CampaignLink>().AsNoTracking()
            .Where(l => db.Set<Campaign>().Any(c => c.Id == l.CampaignId && c.PublicId == campaignPublicId))
            .ToListAsync();
        var tokens = new ClickTrackingTokenService(Options.Create(Marketing));

        return messages.ToDictionary(
            m => m.ToEmail,
            m => TrackedUrl.Matches(m.HtmlBody!)
                .Select(match => match.Groups["token"].Value)
                .Distinct()
                .ToDictionary(token => links.Single(l => l.Id == tokens.TryParse(token)!.LinkId).Url));
    }

    private async Task MarkDeliveredAsync(Guid campaignPublicId)
    {
        await using var db = _fixture.CreateDbContext();
        var prefix = $"{campaignPublicId}:%";
        await db.Database.ExecuteSqlAsync($"""
            UPDATE email.email_outbox_messages SET "Status" = 'Sent', "SentAt" = now()
            WHERE "Purpose" = 'CampaignBroadcast' AND "CorrelationKey" LIKE {prefix}
            """);
    }

    private Task<CampaignDetailDto> DetailAsync(TestData.SeededCreator creator, Guid publicId) =>
        ReadServiceAsync(s => s.GetAsync(creator.Slug, creator.OwnerUserId, publicId, None));

    private static string Email(string name) => TestData.UniqueEmail(name);

    // ---- tests ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task EachRecipient_GetsTheirOwnTrackedUrls_InBothBodies_AndTheUnsubscribeLinkIsUntouched()
    {
        var (ana, ben) = (Email("ana"), Email("ben"));
        var (_, campaign) = await SendAsync(ana, ben);

        await using var db = _fixture.CreateDbContext();
        var prefix = campaign.PublicId + ":";
        var messages = await db.Set<EmailOutboxMessage>().AsNoTracking().Where(m => m.CorrelationKey.StartsWith(prefix)).ToListAsync();
        var tokens = await TokensByRecipientAsync(campaign.PublicId);

        Assert.Equal(2, messages.Count);
        foreach (var message in messages)
        {
            var mine = tokens[message.ToEmail];
            Assert.Equal(["https://blog.test/archive", "https://blog.test/post", "https://shop.test/buy"], mine.Keys.Order());
            Assert.All(mine.Values, token => Assert.Contains($"/api/public/c/{token}", message.PlainTextBody));
            Assert.DoesNotContain("href=\"https://blog.test", message.HtmlBody);
            Assert.DoesNotContain("https://shop.test", message.PlainTextBody);
            Assert.Contains("https://app.luma.test/unsubscribe/", message.HtmlBody);
            Assert.Contains("https://app.luma.test/unsubscribe/", message.PlainTextBody);
            Assert.Contains("/api/public/o/", message.HtmlBody);
        }
        Assert.Empty(tokens[ana].Values.Intersect(tokens[ben].Values));
    }

    [Fact]
    public async Task TheLinksAreStoredOnce_InOrder_WithKindAndLabel()
    {
        var (creator, campaign) = await SendAsync(Email("ana"), Email("ben"));

        var detail = await DetailAsync(creator, campaign.PublicId);

        Assert.True(detail.ClickTracked);
        Assert.Equal(
        [
            new CampaignLinkStatsDto("https://blog.test/post", 1, "Text", null, 0, 0),
            new CampaignLinkStatsDto("https://blog.test/archive", 2, "Text", null, 0, 0),
            new CampaignLinkStatsDto("https://shop.test/buy", 3, "Button", "Shop now", 0, 0)
        ], detail.Clicks.Links);
    }

    [Fact]
    public async Task Counters_AreRightForRepeatedClicks_TwoLinks_AndTwoRecipients()
    {
        var (ana, ben, cid) = (Email("ana"), Email("ben"), Email("cid"));
        var (creator, campaign) = await SendAsync(ana, ben, cid);
        await MarkDeliveredAsync(campaign.PublicId);
        var tokens = await TokensByRecipientAsync(campaign.PublicId);

        Assert.Equal("https://blog.test/post", await ClickAsync(tokens[ana]["https://blog.test/post"]));
        await ClickAsync(tokens[ana]["https://blog.test/post"]);
        Assert.Equal("https://shop.test/buy", await ClickAsync(tokens[ana]["https://shop.test/buy"]));
        await ClickAsync(tokens[ben]["https://blog.test/post"]);

        var detail = await DetailAsync(creator, campaign.PublicId);
        var list = await ReadServiceAsync(s => s.ListAsync(creator.Slug, creator.OwnerUserId, None));

        Assert.Equal((2, 4, 0.6667), (detail.Clicks.UniqueClickers, detail.Clicks.TotalClicks, detail.Clicks.ClickRate));
        Assert.Equal([(2, 3), (0, 0), (1, 1)], detail.Clicks.Links.Select(l => (l.UniqueClicks, l.TotalClicks)));
        var item = Assert.Single(list);
        Assert.Equal((true, 3, 2), (item.ClickTracked, item.LinkCount, item.UniqueClickCount));
        Assert.Equal(
            [(ana, "https://blog.test/post", 2), (ana, "https://shop.test/buy", 1), (ben, "https://blog.test/post", 1)],
            (await ClickRowsAsync(campaign.PublicId)).Select(r => (r.Email, r.Url, r.ClickCount)));
        Assert.Equal([ana, ben], (await FirstClickersAsync(campaign.PublicId)).Order());
    }

    [Fact]
    public async Task ParallelFirstClicks_CountTheRecipientOnce()
    {
        var ana = Email("ana");
        var (creator, campaign) = await SendAsync(ana);
        var token = (await TokensByRecipientAsync(campaign.PublicId))[ana]["https://shop.test/buy"];

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => ClickAsync(token)));

        var detail = await DetailAsync(creator, campaign.PublicId);
        var button = detail.Clicks.Links.Single(l => l.Kind == "Button");
        Assert.Equal((1, 10), (detail.Clicks.UniqueClickers, detail.Clicks.TotalClicks));
        Assert.Equal((1, 10), (button.UniqueClicks, button.TotalClicks));
    }

    [Fact]
    public async Task TheRedirectTarget_ComesFromTheDatabase()
    {
        var ana = Email("ana");
        var (_, campaign) = await SendAsync(ana);
        var token = (await TokensByRecipientAsync(campaign.PublicId))[ana]["https://shop.test/buy"];
        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""
                UPDATE marketing.campaign_links SET "Url" = 'https://shop.test/moved'
                WHERE "Url" = 'https://shop.test/buy' AND "CampaignId" = (SELECT "Id" FROM marketing.campaigns WHERE "PublicId" = {campaign.PublicId})
                """);

        Assert.Equal("https://shop.test/moved", await ClickAsync(token));
    }

    [Fact]
    public async Task ATokenPairingARecipientWithAnotherCampaignsLink_RedirectsNowhere_AndCountsNothing()
    {
        var (ana, ben) = (Email("ana"), Email("ben"));
        var (creator, first) = await SendAsync(ana);
        var (_, second) = await SendAsync(ben);
        var anaRecipientId = new ClickTrackingTokenService(Options.Create(Marketing))
            .TryParse((await TokensByRecipientAsync(first.PublicId))[ana].Values.First())!.RecipientId;
        var benLinkId = new ClickTrackingTokenService(Options.Create(Marketing))
            .TryParse((await TokensByRecipientAsync(second.PublicId))[ben]["https://shop.test/buy"])!.LinkId;
        var forgedPairing = new ClickTrackingTokenService(Options.Create(Marketing)).Create(anaRecipientId, benLinkId);

        Assert.Null(await ClickAsync(forgedPairing));
        Assert.Null(await ClickAsync("not.a-token"));
        Assert.Equal(0, (await DetailAsync(creator, first.PublicId)).Clicks.TotalClicks);
        Assert.Empty(await ClickRowsAsync(second.PublicId));
    }

    [Fact]
    public async Task ACampaignFromBeforeClickTracking_StillLoads()
    {
        var creator = await _data.CreateCreatorAsync();
        Campaign old;
        await using (var db = _fixture.CreateDbContext())
        {
            old = Campaign.CreateQueued(creator.CreatorId, null, "Old", "Body https://old.test", null, null, CampaignAudienceType.All, null, null, 1, DateTimeOffset.UtcNow.AddDays(-40));
            db.Add(old);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlAsync($"""UPDATE marketing.campaigns SET "ClickTracked" = false WHERE "Id" = {old.Id}""");
        }

        var detail = await DetailAsync(creator, old.PublicId);
        var item = Assert.Single(await ReadServiceAsync(s => s.ListAsync(creator.Slug, creator.OwnerUserId, None)));

        Assert.False(detail.ClickTracked);
        Assert.Equal((0, 0, 0d), (detail.Clicks.UniqueClickers, detail.Clicks.TotalClicks, detail.Clicks.ClickRate));
        Assert.Empty(detail.Clicks.Links);
        Assert.Equal((false, 0, 0), (item.ClickTracked, item.LinkCount, item.UniqueClickCount));
    }

    [Fact]
    public async Task AScheduledSend_CreatesItsLinksAtDispatch()
    {
        var creator = await _data.CreateCreatorAsync();
        var ana = Email("ana");
        await AddContactsAsync(creator.CreatorId, [ana]);
        var scheduled = await SendServiceAsync(s => s.SendAsync(creator.Slug, creator.OwnerUserId, new SendCampaignRequestDto
        {
            Subject = "Later", BodyText = "See https://later.test", AudienceType = "All", ScheduledAt = DateTimeOffset.UtcNow.AddHours(1)
        }, None));
        Assert.False(scheduled.ClickTracked);
        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""
                UPDATE marketing.campaigns SET "ScheduledAt" = now() - interval '1 minute' WHERE "PublicId" = {scheduled.PublicId}
                """);

        await SendServiceAsync(async s => { await s.DispatchScheduledAsync(scheduled.PublicId, None); return 0; });
        var detail = await DetailAsync(creator, scheduled.PublicId);
        var tokens = await TokensByRecipientAsync(scheduled.PublicId);

        Assert.Equal(("Queued", true), (detail.Status, detail.ClickTracked));
        Assert.Equal("https://later.test", Assert.Single(detail.Clicks.Links).Url);
        Assert.Equal("https://later.test", await ClickAsync(tokens[ana]["https://later.test"]));
    }

    private sealed record ClickRow(string Email, string Url, int ClickCount);

    private async Task<List<ClickRow>> ClickRowsAsync(Guid campaignPublicId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<ClickRow>($"""
            SELECT r."Email" AS "Email", l."Url" AS "Url", c."ClickCount" AS "ClickCount"
            FROM marketing.campaign_link_clicks c
            JOIN marketing.campaign_links l ON l."Id" = c."LinkId"
            JOIN marketing.campaign_recipients r ON r."Id" = c."RecipientId"
            JOIN marketing.campaigns k ON k."Id" = l."CampaignId"
            WHERE k."PublicId" = {campaignPublicId}
            ORDER BY r."Email", l."Position"
            """).ToListAsync();
    }

    private async Task<List<string>> FirstClickersAsync(Guid campaignPublicId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<string>($"""
            SELECT r."Email" AS "Value"
            FROM marketing.campaign_recipients r
            JOIN marketing.campaigns k ON k."Id" = r."CampaignId"
            WHERE k."PublicId" = {campaignPublicId} AND r."FirstClickedAt" IS NOT NULL
            """).ToListAsync();
    }
}
