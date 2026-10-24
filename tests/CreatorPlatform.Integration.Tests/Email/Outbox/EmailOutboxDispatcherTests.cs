using System.Diagnostics;
using CreatorPlatform.Email.Application.Exceptions;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Outbox;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Email.Outbox;

/// <summary>EMAIL-1 (B7): transactional mail first, fair sharing between creators, bounded parallel sends, the provider
/// quota, the pause after a 429, several Worker instances, and a stop in the middle of a batch. Each test has a database
/// of its own, so its outbox holds only its own mail.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EmailOutboxDispatcherTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedDatabase _database = null!;

    public EmailOutboxDispatcherTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync() => _database = await IsolatedDatabase.CreateAsync(_postgres);

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private OutboxHarness Harness(EmailOutboxOptions options, TimeProvider? clock = null, params IEmailSendFailureHandler[] handlers) =>
        new(_database, options, clock ?? OutboxClock.Now(), failureHandlers: handlers);

    [Fact]
    public async Task TransactionalMail_GoesFirst_EvenBehindABigCampaign()
    {
        await OutboxQueue.CampaignAsync(_database, creatorId: 1, "campaign", 30);
        var orderEmail = await OutboxQueue.TransactionalAsync(_database, "order", EmailOutboxMessagePurpose.OrderAccess);
        var resetEmail = await OutboxQueue.TransactionalAsync(_database, "reset", EmailOutboxMessagePurpose.PasswordReset);
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 10));

        await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None);

        Assert.Equal([orderEmail, resetEmail], outbox.Sender.Sent.Take(2));
        Assert.Equal(10, outbox.Sender.Sent.Count);
    }

    [Fact]
    public async Task ASmallCreatorsCampaign_IsNotStuckBehindABigOne()
    {
        await OutboxQueue.CampaignAsync(_database, creatorId: 1, "big", 40);
        var small = await OutboxQueue.CampaignAsync(_database, creatorId: 2, "small", 10);
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 10));

        await outbox.DrainAsync();

        var firstTwenty = outbox.Sender.Sent.Take(20).ToList();
        Assert.All(small, to => Assert.Contains(to, firstTwenty));   // alternating with the big one from the start
        Assert.Equal(50, outbox.Sender.Sent.Count);
    }

    [Fact]
    public async Task ACreatorsSecondCampaign_WaitsBehindItsFirst_InsteadOfDoublingItsShare()
    {
        var first = await OutboxQueue.CampaignAsync(_database, creatorId: 1, "a-first", 20);
        var second = await OutboxQueue.CampaignAsync(_database, creatorId: 1, "a-second", 20);
        var other = await OutboxQueue.CampaignAsync(_database, creatorId: 2, "b", 20);
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 10));

        await outbox.DrainAsync();

        var firstForty = outbox.Sender.Sent.Take(40).ToHashSet();
        Assert.Subset(firstForty, first.Concat(other).ToHashSet());   // one share each for creator 1 and creator 2
        Assert.DoesNotContain(second, firstForty.Contains);
        Assert.Equal(60, outbox.Sender.Sent.Count);
    }

    [Fact]
    public async Task Sends_RunInParallel_UpToTheLimit_AndNeverBeyond()
    {
        await OutboxQueue.CampaignAsync(_database, creatorId: 1, "parallel", 24);
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 12, parallel: 4));
        outbox.Sender.Delay = TimeSpan.FromMilliseconds(150);

        var stopwatch = Stopwatch.StartNew();
        await outbox.DrainAsync();

        Assert.Equal(24, outbox.Sender.Sent.Count);
        Assert.Equal(4, outbox.Sender.MaxInFlight);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(24 * 150), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task ThePerMinuteQuota_IsNeverExceeded_AndTransactionalMailKeepsItsReserve()
    {
        await OutboxQueue.CampaignAsync(_database, creatorId: 1, "campaign", 10);
        var clock = OutboxClock.Now();
        // 5 per minute, 20% (one) kept for transactional mail.
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 10, perMinute: 5, perHour: 1000, reservePercent: 20), clock);

        Assert.Equal(4, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
        var idle = await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None);
        Assert.Equal(0, idle.Claimed);    // the campaign has used its share of the minute

        var order = await OutboxQueue.TransactionalAsync(_database, "order");
        Assert.Equal(1, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
        Assert.Equal(order, outbox.Sender.Sent.Last());
        Assert.Equal(0, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);   // minute full

        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(4, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
        Assert.Equal(9, outbox.Sender.Sent.Count);
    }

    [Fact]
    public async Task ThePerHourQuota_HoldsAcrossMinutes_AndSaysWhenItFreesUp()
    {
        await OutboxQueue.CampaignAsync(_database, creatorId: 1, "campaign", 30);
        var clock = OutboxClock.Now();
        var start = clock.GetUtcNow();
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 50, perMinute: 10, perHour: 12), clock);

        Assert.Equal(10, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(2, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(0, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
        Assert.Equal(start + TimeSpan.FromHours(1), outbox.Throttle.GetBudget().NextPermitAt);

        clock.Advance(start + TimeSpan.FromHours(1) - clock.GetUtcNow());
        Assert.Equal(10, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
    }

    [Fact]
    public async Task AProvider429_PausesAllSending_AndTheMessageKeepsItsPlaceAndItsRetries()
    {
        var recipients = await OutboxQueue.CampaignAsync(_database, creatorId: 1, "throttled", 3);
        var clock = OutboxClock.Now();
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 1), clock);
        var throttleOnce = 1;
        outbox.Sender.FailWith = _ => Interlocked.Exchange(ref throttleOnce, 0) == 1 ? HeaderOnlyResponse.Throttled(retryAfterSeconds: 30) : null;

        await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None);

        var deferred = await OutboxQueue.GetAsync(_database, recipients[0]);
        Assert.Equal((EmailOutboxMessageStatus.Processing, 0), (deferred.Status, deferred.RetryCount));
        Assert.Equal(clock.GetUtcNow().AddSeconds(30), deferred.ProcessingExpiresAt);

        var paused = await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None);
        Assert.Equal(0, paused.Claimed);
        Assert.InRange(paused.IdleFor, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));

        clock.Advance(TimeSpan.FromSeconds(31));
        await outbox.DrainAsync();

        Assert.Equal(recipients, outbox.Sender.Sent);   // the throttled one first: it kept its place
    }

    [Fact]
    public async Task AnUnreachableProvider_IsTreatedLikeThrottling()
    {
        var recipients = await OutboxQueue.CampaignAsync(_database, creatorId: 1, "down", 2);
        var clock = OutboxClock.Now();
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 2), clock);
        outbox.Sender.FailWith = _ => new TransientEmailSendException("down", TimeSpan.FromMinutes(1));

        await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None);

        foreach (var to in recipients)
            Assert.Equal(0, (await OutboxQueue.GetAsync(_database, to)).RetryCount);
        Assert.Equal(0, outbox.Throttle.GetBudget().Transactional);
    }

    [Fact]
    public async Task TwoWorkerInstances_SendEveryMessageExactlyOnce()
    {
        var recipients = await OutboxQueue.CampaignAsync(_database, creatorId: 1, "replicas", 60);
        await using var first = Harness(OutboxHarness.Options(batchSize: 7, parallel: 3));
        await using var second = Harness(OutboxHarness.Options(batchSize: 7, parallel: 3));
        first.Sender.Delay = second.Sender.Delay = TimeSpan.FromMilliseconds(10);

        await Task.WhenAll(first.DrainAsync(), second.DrainAsync());

        var sent = first.Sender.Sent.Concat(second.Sender.Sent).ToList();
        Assert.Equal(recipients.Order(), sent.Order());
        Assert.NotEmpty(first.Sender.Sent);
        Assert.NotEmpty(second.Sender.Sent);
    }

    [Fact]
    public async Task AStopDuringABatch_FinishesWhatWasClaimed()
    {
        var recipients = await OutboxQueue.CampaignAsync(_database, creatorId: 1, "stopping", 4);
        await using var outbox = Harness(OutboxHarness.Options(batchSize: 4, parallel: 2));
        outbox.Sender.Delay = TimeSpan.FromMilliseconds(300);
        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await outbox.Dispatcher.DispatchOnceAsync(stopping.Token);

        Assert.Equal(4, result.Claimed);
        foreach (var to in recipients)
            Assert.Equal(EmailOutboxMessageStatus.Sent, (await OutboxQueue.GetAsync(_database, to)).Status);
    }

    private sealed class CountingFailureHandler : IEmailSendFailureHandler
    {
        public int Calls;

        public EmailOutboxMessagePurpose Purpose => EmailOutboxMessagePurpose.CampaignBroadcast;

        public Task HandleAsync(EmailOutboxMessage message, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AMessageThatKeepsFailing_IsRetriedThreeTimes_ThenFails_AndItsHandlerRunsOnce()
    {
        var to = (await OutboxQueue.CampaignAsync(_database, creatorId: 1, "bounce", 1)).Single();
        var clock = OutboxClock.Now();
        var handler = new CountingFailureHandler();
        await using var outbox = Harness(OutboxHarness.Options(), clock, handler);
        outbox.Sender.FailWith = _ => new InvalidOperationException("mailbox unavailable");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(1, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
            clock.Advance(TimeSpan.FromMinutes(16));
        }

        var failed = await OutboxQueue.GetAsync(_database, to);
        Assert.Equal((EmailOutboxMessageStatus.Failed, 3, "mailbox unavailable"), (failed.Status, failed.RetryCount, failed.LastError));
        Assert.Equal(1, handler.Calls);
        Assert.Equal(0, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
    }

    [Fact]
    public async Task ACancelledMessage_IsNotSent()
    {
        var recipients = await OutboxQueue.CampaignAsync(_database, creatorId: 1, "cancel", 2);
        await using (var db = _database.CreateDbContext())
        {
            var message = db.Set<EmailOutboxMessage>().Single(row => row.ToEmail == recipients[0]);
            message.Cancel();
            await db.SaveChangesAsync();
        }

        await using var outbox = Harness(OutboxHarness.Options());
        await outbox.DrainAsync();

        Assert.Equal([recipients[1]], outbox.Sender.Sent);
    }

    [Fact]
    public async Task AMessageDeletedWhileBeingSent_IsSkipped_NotLoggedAsAFailure()
    {
        // Deleting the workspace deletes its mail, also a message the dispatcher has already claimed.
        var to = await OutboxQueue.TransactionalAsync(_database, "deleted-workspace");
        var sender = new RecordingEmailSender
        {
            FailWith = recipient =>
            {
                using var db = _database.CreateDbContext();
                db.Database.ExecuteSql($"DELETE FROM email.email_outbox_messages WHERE \"ToEmail\" = {recipient}");
                return null;
            }
        };
        var logger = new ListLogger<EmailOutboxDispatcher>();
        await using var outbox = new OutboxHarness(_database, OutboxHarness.Options(), OutboxClock.Now(), sender, logger);

        await outbox.DrainAsync();

        Assert.Equal([to], sender.Sent);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Equal(0, (await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None)).Claimed);
    }
}

/// <summary>EMAIL-1: the outbox loop does not pause while messages are ready.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EmailOutboxProcessorTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedDatabase _database = null!;

    public EmailOutboxProcessorTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync() => _database = await IsolatedDatabase.CreateAsync(_postgres);

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task WhileWorkRemains_PassesFollowEachOther_WithoutThePollInterval()
    {
        await OutboxQueue.CampaignAsync(_database, creatorId: 1, "backlog", 23);
        var options = OutboxHarness.Options(batchSize: 5);
        options.PollIntervalSeconds = 60;   // one pause would fail the test
        await using var outbox = new OutboxHarness(_database, options, TimeProvider.System);
        var processor = new EmailOutboxProcessor(outbox.Dispatcher, Options.Create(options), TimeProvider.System, NullLogger<EmailOutboxProcessor>.Instance);

        await processor.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (outbox.Sender.Sent.Count < 23 && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        await processor.StopAsync(CancellationToken.None);

        Assert.Equal(23, outbox.Sender.Sent.Count);
    }
}
