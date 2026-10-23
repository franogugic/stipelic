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
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Marketing.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Marketing;

/// <summary>
/// Resending a campaign to the people who did not open it (F1.1), on the real send path and database: who gets
/// it, every eligibility rule, the 5-resend cap under concurrency, the monthly limit and ownership.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NonOpenerResendTests
{
    private const string MonthlySendsKey = "max_email_sends_per_month";
    private const string NonOpeners = nameof(CampaignAudienceType.CampaignNonOpeners);
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public NonOpenerResendTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private enum Delivery
    {
        Sent,
        Failed,
        Pending
    }

    private sealed record Person(string Email, Delivery Delivery = Delivery.Sent, bool Opened = false);

    // ---- harness ----------------------------------------------------------------------------------------------

    private static readonly MarketingOptions Marketing = new()
    {
        UnsubscribeTokenSecret = "test-secret-value-1234567890",
        OpenTrackingSecret = "test-open-secret-value-1234567890",
        ClickTrackingSecret = "test-click-secret-value-1234567890",
        ApiBaseUrl = "http://localhost:5000",
        FrontendBaseUrl = "https://app.luma.test",
    };

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

    private Task<CampaignDetailDto> ResendAsync(
        TestData.SeededCreator creator, Guid originalPublicId, DateTimeOffset? scheduledAt = null, string subject = "Did you miss this?") =>
        SendServiceAsync(s => s.SendAsync(creator.Slug, creator.OwnerUserId, new SendCampaignRequestDto
        {
            Subject = subject,
            BodyText = "Body",
            AudienceType = NonOpeners,
            TargetPublicId = originalPublicId,
            ScheduledAt = scheduledAt
        }, None));

    private Task<CampaignDetailDto> DetailAsync(TestData.SeededCreator creator, Guid publicId) =>
        ReadServiceAsync(s => s.GetAsync(creator.Slug, creator.OwnerUserId, publicId, None));

    private async Task<string> ResendRefusedAsync(TestData.SeededCreator creator, Guid originalPublicId, DateTimeOffset? scheduledAt = null)
    {
        var refused = await Assert.ThrowsAsync<ConflictException>(() => ResendAsync(creator, originalPublicId, scheduledAt));
        return refused.Code;
    }

    // ---- seeding ----------------------------------------------------------------------------------------------

    private static string Email(string name) => TestData.UniqueEmail(name);

    /// <summary>A sent original (or, with <paramref name="source"/>, a sent resend of it) queued at
    /// <paramref name="queuedAt"/>, with one recipient row and outbox message per person.</summary>
    private async Task<Campaign> SeedSentAsync(
        int creatorId, DateTimeOffset queuedAt, IReadOnlyList<Person> people, Campaign? source = null, int? round = null)
    {
        // Whole seconds: Postgres keeps microseconds, so a tick-precise time would not round-trip.
        queuedAt = new DateTimeOffset(queuedAt.UtcTicks - queuedAt.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        await using var db = _fixture.CreateDbContext();
        var campaign = Campaign.CreateQueued(
            creatorId, null, source is null ? "Original subject" : $"Resend {round}", "Body", null, null,
            source is null ? CampaignAudienceType.All : CampaignAudienceType.CampaignNonOpeners,
            null, null, people.Count, queuedAt, source?.Id, source is null ? null : round);
        db.Add(campaign);
        await db.SaveChangesAsync();

        foreach (var person in people)
        {
            var recipient = CampaignRecipient.Create(campaign.Id, person.Email, queuedAt);
            db.Add(recipient);
            await db.SaveChangesAsync();

            var message = EmailOutboxMessage.Create(
                EmailOutboxMessagePurpose.CampaignBroadcast, $"{campaign.PublicId}:{recipient.Id}", person.Email,
                "Subject", "<p>Body</p>", "Body", queuedAt);
            if (person.Delivery == Delivery.Sent)
                message.MarkAsSent(queuedAt);
            else if (person.Delivery == Delivery.Failed)
                message.MarkAsFailed("bounced", queuedAt, maxRetryCount: 1);
            db.Add(message);

            if (person.Opened)
                await OpenAsync(campaign.Id, person.Email);
        }

        await db.SaveChangesAsync();
        return campaign;
    }

    /// <summary>What the pixel endpoint does: stamps the recipient and counts the open on the campaign.</summary>
    private async Task OpenAsync(int campaignId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            WITH opened AS (
                UPDATE marketing.campaign_recipients SET "FirstOpenedAt" = now()
                WHERE "CampaignId" = {campaignId} AND "Email" = {email.ToLowerInvariant()} AND "FirstOpenedAt" IS NULL
                RETURNING 1)
            UPDATE marketing.campaigns SET "UniqueOpenCount" = "UniqueOpenCount" + (SELECT count(*) FROM opened)
            WHERE "Id" = {campaignId}
            """);
    }

    private async Task UnsubscribeAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        db.Add(Unsubscribe.Create(creatorId, email, UnsubscribeSource.Link, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    /// <summary>The outbox worker delivered every message of the campaign; it went out <paramref name="sentAt"/>.</summary>
    private async Task DeliverAsync(Guid campaignPublicId, DateTimeOffset sentAt)
    {
        await using var db = _fixture.CreateDbContext();
        var prefix = $"{campaignPublicId}:%";
        await db.Database.ExecuteSqlAsync($"""
            UPDATE email.email_outbox_messages SET "Status" = 'Sent', "SentAt" = {sentAt}
            WHERE "Purpose" = 'CampaignBroadcast' AND "CorrelationKey" LIKE {prefix}
            """);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE marketing.campaigns SET "QueuedAt" = {sentAt}, "CreatedAt" = {sentAt} WHERE "PublicId" = {campaignPublicId}
            """);
    }

    private async Task<List<string>> OutboxRecipientsAsync(Guid campaignPublicId)
    {
        await using var db = _fixture.CreateDbContext();
        var prefix = campaignPublicId + ":";
        return await db.Set<EmailOutboxMessage>().AsNoTracking()
            .Where(m => m.CorrelationKey.StartsWith(prefix))
            .Select(m => m.ToEmail)
            .OrderBy(e => e)
            .ToListAsync();
    }

    private async Task<int> UsedAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return await new CreatorUsageService(db).GetUsedAsync(creatorId, MonthlySendsKey, UsagePeriod.CalendarMonth, None);
    }

    private async Task<int> ResendCountAsync(int originalId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Set<Campaign>().CountAsync(c => c.SourceCampaignId == originalId);
    }

    private static DateTimeOffset DaysAgo(double days) => DateTimeOffset.UtcNow.AddDays(-days);

    // ---- who gets the resend ----------------------------------------------------------------------------------

    [Fact]
    public async Task OpenersFailedDeliveriesUnsubscribedAndUndelivered_AreLeftOut()
    {
        var creator = await _data.CreateCreatorAsync();
        var (opener, failed, unsubscribed, pending, first, second) =
            (Email("opener"), Email("failed"), Email("unsub"), Email("pending"), Email("a-reach"), Email("b-reach"));
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(2),
        [
            new(opener, Opened: true), new(failed, Delivery.Failed), new(unsubscribed), new(pending, Delivery.Pending),
            new(first), new(second)
        ]);
        await UnsubscribeAsync(creator.CreatorId, unsubscribed);
        var usedBefore = await UsedAsync(creator.CreatorId);

        var preview = await ReadServiceAsync(s => s.GetAudiencePreviewAsync(
            creator.Slug, creator.OwnerUserId, CampaignAudienceType.CampaignNonOpeners, original.PublicId, None));
        var page = await ReadServiceAsync(s => s.GetAudienceRecipientsAsync(
            creator.Slug, creator.OwnerUserId, CampaignAudienceType.CampaignNonOpeners, original.PublicId, null, 1, None));
        var resend = await ResendAsync(creator, original.PublicId);

        Assert.Equal(2, preview.RecipientCount);
        Assert.Equal([first], page.Emails);
        Assert.True(page.HasMore);
        Assert.Equal("Queued", resend.Status);
        Assert.Equal(NonOpeners, resend.AudienceType);
        Assert.Equal(2, resend.RecipientCount);
        Assert.Equal(1, resend.ResendRound);
        Assert.Equal(original.PublicId, resend.SourceCampaignPublicId);
        Assert.Equal(original.PublicId, resend.TargetPublicId);
        Assert.Equal(new CampaignSourceDto(original.PublicId, "Original subject"), resend.Source);
        Assert.Equal(NonOpenerResendReasons.IsResend, resend.NonOpenerResend.Reason);
        Assert.Equal([first, second], await OutboxRecipientsAsync(resend.PublicId));
        Assert.Equal(usedBefore + 2, await UsedAsync(creator.CreatorId));
    }

    [Fact]
    public async Task AnOpenerOfTheSecondResend_IsNotInTheThird_NorIsAFailedDeliveryOfAnEarlierResend()
    {
        var creator = await _data.CreateCreatorAsync();
        var (openedSecond, failedSecond, stillQuiet, alsoQuiet, openedFirst) =
            (Email("a"), Email("b"), Email("c"), Email("d"), Email("e"));
        var everyone = new[] { openedSecond, failedSecond, stillQuiet, alsoQuiet, openedFirst };
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(5), everyone.Select(e => new Person(e)).ToList());
        await SeedSentAsync(creator.CreatorId, DaysAgo(4), everyone.Select(e => new Person(e, Opened: e == openedFirst)).ToList(), original, 1);
        await SeedSentAsync(creator.CreatorId, DaysAgo(3),
        [
            new(openedSecond, Opened: true), new(failedSecond, Delivery.Failed), new(stillQuiet), new(alsoQuiet)
        ], original, 2);

        var third = await ResendAsync(creator, original.PublicId);

        Assert.Equal(3, third.ResendRound);
        Assert.Equal(new[] { stillQuiet, alsoQuiet }.Order(), await OutboxRecipientsAsync(third.PublicId));
    }

    [Fact]
    public async Task TheOriginalsDetail_ShowsEveryResendWithItsNumbers()
    {
        var creator = await _data.CreateCreatorAsync();
        var people = Enumerable.Range(0, 4).Select(i => Email($"p{i}")).ToList();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(3),
            people.Select((e, i) => new Person(e, Opened: i == 0)).ToList());
        var first = await SeedSentAsync(creator.CreatorId, DaysAgo(2),
            people.Skip(1).Select((e, i) => new Person(e, i == 2 ? Delivery.Failed : Delivery.Sent, Opened: i == 0)).ToList(), original, 1);

        var detail = await DetailAsync(creator, original.PublicId);
        var list = await ReadServiceAsync(s => s.ListAsync(creator.Slug, creator.OwnerUserId, None));

        var state = detail.NonOpenerResend;
        Assert.True(state.Eligible);
        Assert.Null(state.Reason);
        Assert.Equal(1, state.RecipientCount); // p2: p0 opened the original, p1 the resend, p3's resend failed
        Assert.Equal(1, state.Used);
        Assert.Equal(5, state.Limit);
        Assert.Equal(2, state.OpenedAnyCount);
        Assert.Equal(1, state.OpenedThroughResendsCount);
        Assert.Equal(original.QueuedAt!.Value.AddDays(30), state.EligibleUntil);
        Assert.Equal(first.QueuedAt!.Value.AddHours(24), state.EligibleFrom);
        var row = Assert.Single(state.Resends);
        Assert.Equal(new NonOpenerResendRowDto(first.PublicId, 1, "Queued", "Resend 1", first.QueuedAt, null, 3, 2, 1), row);
        Assert.Null(detail.SourceCampaignPublicId);
        Assert.Null(detail.ResendRound);

        var resendItem = list.Single(c => c.PublicId == first.PublicId);
        var originalItem = list.Single(c => c.PublicId == original.PublicId);
        Assert.Equal((original.PublicId, 1, "Original subject"), (resendItem.SourceCampaignPublicId!.Value, resendItem.ResendRound!.Value, resendItem.Source!.Subject));
        Assert.Equal((null, null, null), (originalItem.SourceCampaignPublicId, originalItem.ResendRound, originalItem.Source));
    }

    // ---- eligibility --------------------------------------------------------------------------------------------

    [Fact]
    public async Task AScheduledOrCancelledOriginal_IsNotSent()
    {
        var creator = await _data.CreateCreatorAsync();
        var scheduled = await SendServiceAsync(s => s.SendAsync(creator.Slug, creator.OwnerUserId, new SendCampaignRequestDto
        {
            Subject = "Later", BodyText = "Body", AudienceType = "All", ScheduledAt = DateTimeOffset.UtcNow.AddDays(1)
        }, None));

        Assert.Equal(NonOpenerResendReasons.NotSent, (await DetailAsync(creator, scheduled.PublicId)).NonOpenerResend.Reason);
        Assert.Equal(NonOpenerResendReasons.NotSent, await ResendRefusedAsync(creator, scheduled.PublicId));
        var preview = await Assert.ThrowsAsync<ConflictException>(() => ReadServiceAsync(s => s.GetAudiencePreviewAsync(
            creator.Slug, creator.OwnerUserId, CampaignAudienceType.CampaignNonOpeners, scheduled.PublicId, None)));
        Assert.Equal(NonOpenerResendReasons.NotSent, preview.Code);
    }

    [Fact]
    public async Task AResend_CannotBeResent()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(3), [new(Email("a"))]);
        var resend = await SeedSentAsync(creator.CreatorId, DaysAgo(2), [new(Email("a"))], original, 1);

        Assert.Equal(NonOpenerResendReasons.IsResend, await ResendRefusedAsync(creator, resend.PublicId));
        Assert.Equal(NonOpenerResendReasons.IsResend, (await DetailAsync(creator, resend.PublicId)).NonOpenerResend.Reason);
    }

    [Fact]
    public async Task Within24HoursOfTheOriginal_ItIsTooEarly()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DateTimeOffset.UtcNow.AddHours(-23), [new(Email("a"))]);

        var state = (await DetailAsync(creator, original.PublicId)).NonOpenerResend;

        Assert.Equal((false, NonOpenerResendReasons.TooEarly), (state.Eligible, state.Reason));
        Assert.Equal(original.QueuedAt!.Value.AddHours(24), state.EligibleFrom);
        Assert.Equal(NonOpenerResendReasons.TooEarly, await ResendRefusedAsync(creator, original.PublicId));
    }

    [Fact]
    public async Task Within24HoursOfThePreviousResend_ItIsTooEarly()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(3), [new(Email("a")), new(Email("b"))]);
        await SeedSentAsync(creator.CreatorId, DateTimeOffset.UtcNow.AddHours(-2), [new(Email("a"))], original, 1);

        Assert.Equal(NonOpenerResendReasons.TooEarly, await ResendRefusedAsync(creator, original.PublicId));
    }

    [Fact]
    public async Task After30Days_ItIsTooLate()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(30.1), [new(Email("a"))]);

        Assert.Equal(NonOpenerResendReasons.TooLate, (await DetailAsync(creator, original.PublicId)).NonOpenerResend.Reason);
        Assert.Equal(NonOpenerResendReasons.TooLate, await ResendRefusedAsync(creator, original.PublicId));
    }

    [Fact]
    public async Task WhenTheNextPossibleTimeIsPastTheWindow_ItIsTooLateAtOnce()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(29.5), [new(Email("a")), new(Email("b"))]);
        await SeedSentAsync(creator.CreatorId, DateTimeOffset.UtcNow.AddHours(-1), [new(Email("a"))], original, 1);

        Assert.Equal(NonOpenerResendReasons.TooLate, (await DetailAsync(creator, original.PublicId)).NonOpenerResend.Reason);
    }

    [Fact]
    public async Task AScheduledResend_IsPending_AndCountsTowardTheFive()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(2), [new(Email("a"))]);

        var scheduled = await ResendAsync(creator, original.PublicId, scheduledAt: DateTimeOffset.UtcNow.AddDays(1));
        var state = (await DetailAsync(creator, original.PublicId)).NonOpenerResend;

        Assert.Equal(("Scheduled", 1), (scheduled.Status, scheduled.ResendRound));
        Assert.Equal((NonOpenerResendReasons.ResendPending, 1), (state.Reason, state.Used));
        Assert.Equal("Scheduled", Assert.Single(state.Resends).Status);
        Assert.Equal(NonOpenerResendReasons.ResendPending, await ResendRefusedAsync(creator, original.PublicId));
    }

    [Fact]
    public async Task AResendScheduledPastTheWindow_IsRefused()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(25), [new(Email("a"))]);

        Assert.Equal(NonOpenerResendReasons.TooLate,
            await ResendRefusedAsync(creator, original.PublicId, scheduledAt: DateTimeOffset.UtcNow.AddDays(6)));
        Assert.Equal(0, await ResendCountAsync(original.Id));
    }

    [Fact]
    public async Task WhenEveryoneOpened_ThereIsNobodyLeft()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(2), [new(Email("a"), Opened: true), new(Email("b"), Delivery.Failed)]);

        Assert.Equal(NonOpenerResendReasons.NoRecipients, (await DetailAsync(creator, original.PublicId)).NonOpenerResend.Reason);
        Assert.Equal(NonOpenerResendReasons.NoRecipients, await ResendRefusedAsync(creator, original.PublicId));
    }

    // ---- the cap ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task TheSixthResend_Is409_AndCancellingAScheduledOneFreesTheSlot()
    {
        var creator = await _data.CreateCreatorAsync();
        var people = Enumerable.Range(0, 3).Select(i => new Person(Email($"p{i}"))).ToList();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(10), people);
        for (var round = 1; round <= 4; round++)
            await SeedSentAsync(creator.CreatorId, DaysAgo(10 - round), people, original, round);

        var fifth = await ResendAsync(creator, original.PublicId, scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        var full = (await DetailAsync(creator, original.PublicId)).NonOpenerResend;
        var sixth = await ResendRefusedAsync(creator, original.PublicId);
        await SendServiceAsync(s => s.CancelScheduledAsync(creator.Slug, creator.OwnerUserId, fifth.PublicId, None));
        var freed = (await DetailAsync(creator, original.PublicId)).NonOpenerResend;
        var again = await ResendAsync(creator, original.PublicId);

        Assert.Equal(5, fifth.ResendRound);
        Assert.Equal((NonOpenerResendReasons.LimitReached, 5), (full.Reason, full.Used));
        Assert.Equal(NonOpenerResendReasons.LimitReached, sixth);
        Assert.Equal((true, 4), (freed.Eligible, freed.Used));
        Assert.Equal(("Queued", 5), (again.Status, again.ResendRound));
        Assert.Equal(6, await ResendCountAsync(original.Id)); // 4 sent + the cancelled one + the new 5th
    }

    [Fact]
    public async Task AFailedScheduledResend_FreesItsSlot()
    {
        var creator = await _data.CreateCreatorAsync();
        var quiet = Email("quiet");
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(3), [new(quiet)]);
        var scheduled = await ResendAsync(creator, original.PublicId, scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        await UnsubscribeAsync(creator.CreatorId, quiet);
        await MakeDueAsync(scheduled.PublicId);

        await SendServiceAsync(async s => { await s.DispatchScheduledAsync(scheduled.PublicId, None); return 0; });
        var state = (await DetailAsync(creator, original.PublicId)).NonOpenerResend;

        Assert.Equal("Failed", Assert.Single(state.Resends).Status);
        Assert.Equal((0, NonOpenerResendReasons.NoRecipients), (state.Used, state.Reason));
    }

    [Fact]
    public async Task TwoParallelRequestsForTheFifthSlot_CreateExactlyOne()
    {
        var creator = await _data.CreateCreatorAsync();
        var people = Enumerable.Range(0, 3).Select(i => new Person(Email($"p{i}"))).ToList();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(10), people);
        for (var round = 1; round <= 4; round++)
            await SeedSentAsync(creator.CreatorId, DaysAgo(10 - round), people, original, round);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            try
            {
                return (await ResendAsync(creator, original.PublicId)).ResendRound?.ToString();
            }
            catch (ConflictException refused)
            {
                return refused.Code;
            }
        }));

        Assert.Equal("5", Assert.Single(attempts, a => a == "5"));
        Assert.All(attempts.Where(a => a != "5"), code => Assert.Equal(NonOpenerResendReasons.LimitReached, code));
        Assert.Equal(5, await ResendCountAsync(original.Id));
    }

    // ---- limits, ownership, dispatch ----------------------------------------------------------------------------

    [Fact]
    public async Task TheMonthlyLimit_Applies()
    {
        var creator = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(2), [new(Email("a")), new(Email("b"))]);
        var limit = await PlanLimitAsync(creator.CreatorId);
        await using (var db = _fixture.CreateDbContext())
        {
            var used = await new CreatorUsageService(db).GetUsedAsync(creator.CreatorId, MonthlySendsKey, UsagePeriod.CalendarMonth, None);
            await new CreatorUsageService(db).TryConsumeAsync(creator.CreatorId, MonthlySendsKey, limit - 1 - used, -1, UsagePeriod.CalendarMonth, None);
        }

        await Assert.ThrowsAsync<ConflictException>(() => ResendAsync(creator, original.PublicId));

        Assert.Equal(0, await ResendCountAsync(original.Id));
        Assert.Equal(limit - 1, await UsedAsync(creator.CreatorId));
    }

    [Fact]
    public async Task AnotherCreatorsCampaign_Is404()
    {
        var owner = await _data.CreateCreatorAsync();
        var intruder = await _data.CreateCreatorAsync();
        var original = await SeedSentAsync(owner.CreatorId, DaysAgo(2), [new(Email("a"))]);

        await Assert.ThrowsAsync<NotFoundException>(() => ResendAsync(intruder, original.PublicId));
        await Assert.ThrowsAsync<NotFoundException>(() => ReadServiceAsync(s => s.GetAudiencePreviewAsync(
            intruder.Slug, intruder.OwnerUserId, CampaignAudienceType.CampaignNonOpeners, original.PublicId, None)));
        await Assert.ThrowsAsync<NotFoundException>(() => DetailAsync(intruder, original.PublicId));
        Assert.Equal(0, await ResendCountAsync(original.Id));
    }

    [Fact]
    public async Task AScheduledResend_ResolvesItsPeopleAtDispatch()
    {
        var creator = await _data.CreateCreatorAsync();
        var (opensLater, staysQuiet) = (Email("later"), Email("quiet"));
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(2), [new(opensLater), new(staysQuiet)]);
        var scheduled = await ResendAsync(creator, original.PublicId, scheduledAt: DateTimeOffset.UtcNow.AddHours(1));
        await OpenAsync(original.Id, opensLater);
        await MakeDueAsync(scheduled.PublicId);

        await SendServiceAsync(async s => { await s.DispatchScheduledAsync(scheduled.PublicId, None); return 0; });
        var dispatched = await DetailAsync(creator, scheduled.PublicId);

        Assert.Equal(("Queued", 1, 1), (dispatched.Status, dispatched.RecipientCount, dispatched.ResendRound));
        Assert.Equal([staysQuiet], await OutboxRecipientsAsync(scheduled.PublicId));
    }

    [Fact]
    public async Task ASentResend_StartsThe24HoursAgain_AndTheNextOneGoesToWhoIsStillQuiet()
    {
        var creator = await _data.CreateCreatorAsync();
        var (opensFirstResend, staysQuiet) = (Email("a"), Email("b"));
        var original = await SeedSentAsync(creator.CreatorId, DaysAgo(3), [new(opensFirstResend), new(staysQuiet)]);

        var first = await ResendAsync(creator, original.PublicId);
        Assert.Equal(NonOpenerResendReasons.TooEarly, (await DetailAsync(creator, original.PublicId)).NonOpenerResend.Reason);

        await DeliverAsync(first.PublicId, DaysAgo(1.5));
        await OpenAsync(await CampaignIdAsync(first.PublicId), opensFirstResend);
        var second = await ResendAsync(creator, original.PublicId);

        Assert.Equal(2, second.ResendRound);
        Assert.Equal([staysQuiet], await OutboxRecipientsAsync(second.PublicId));
    }

    private async Task MakeDueAsync(Guid campaignPublicId)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            UPDATE marketing.campaigns SET "ScheduledAt" = now() - interval '1 minute' WHERE "PublicId" = {campaignPublicId}
            """);
    }

    private async Task<int> CampaignIdAsync(Guid publicId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Set<Campaign>().Where(c => c.PublicId == publicId).Select(c => c.Id).SingleAsync();
    }

    private async Task<int> PlanLimitAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await new MarketingCreatorContextProvider(db).GetActivePlanLimitAsync(creatorId, MonthlySendsKey, None))!.Value;
    }
}
