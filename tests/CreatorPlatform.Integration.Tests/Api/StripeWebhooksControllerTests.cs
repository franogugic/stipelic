using System.Security.Cryptography;
using System.Text;
using CreatorPlatform.Api.Controllers;
using CreatorPlatform.Api.Webhooks;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Payments.Domain;
using CreatorPlatform.Payments.Infrastructure.Repositories;
using CreatorPlatform.Payments.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Infrastructure.Persistence.Migrations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>
/// B1 through the real webhook endpoint (signed payloads, real parsing, real failure table): an outage answers 500 so
/// Stripe retries, and its retries count on one failure row that the first success resolves; a permanent failure answers
/// 200, is recorded and logged for alerting; recording never saves what the failed handler left in the request's
/// DbContext; an admin lists, replays and resolves failures. Also the routing of Connect and expired-session events.
/// B5: an event id is claimed once — a redelivery or a concurrent delivery never runs the handler twice, a failure gives
/// the claim back, a stale claim is taken over.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class StripeWebhooksControllerTests : IAsyncDisposable
{
    private const string Secret = "whsec_test_platform";
    private const string ConnectSecret = "whsec_test_connect";

    private readonly PostgresFixture _fixture;
    private readonly ServiceProvider _services;
    private readonly AsyncServiceScope _requestScope;
    private readonly ScriptedOrderWebhooks _orders = new();
    private readonly RecordingCreatorWebhooks _creators = new();
    private readonly EventLogger<StripeWebhookProcessor> _log = new();
    private readonly StripeWebhookService _stripe = new(
        Options.Create(new StripeOptions { WebhookSecret = Secret, ConnectWebhookSecret = ConnectSecret }),
        NullLogger<StripeWebhookService>.Instance);

    public StripeWebhooksControllerTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _services = new ServiceCollection()
            .AddDbContext<CreatorPlatformDbContext>(options => options.UseNpgsql(fixture.ConnectionString))
            .AddScoped<IWebhookFailureRepository, WebhookFailureRepository>()
            .AddScoped<IProcessedWebhookEventStore, ProcessedWebhookEventStore>()
            .BuildServiceProvider();
        _requestScope = _services.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        await _requestScope.DisposeAsync();
        await _services.DisposeAsync();
    }

    private StripeWebhookProcessor Processor() => new(
        _creators, _orders, _services.GetRequiredService<IServiceScopeFactory>(), _log);

    private async Task<int> PostAsync(string payload, bool connect = false)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var key = Encoding.UTF8.GetBytes(connect ? ConnectSecret : Secret);
        var signature = Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        context.Request.Headers["Stripe-Signature"] = $"t={timestamp},v1={signature}";
        var controller = new StripeWebhooksController(_stripe, Processor(), NullLogger<StripeWebhooksController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = connect ? await controller.HandleConnect(CancellationToken.None) : await controller.Handle(CancellationToken.None);
        return ((IStatusCodeActionResult)result).StatusCode ?? 200;
    }

    private static string NewEventId() => $"evt_test_{Guid.NewGuid():N}";

    private static string Event(string id, string type, string dataObject, string? account = null)
    {
        var accountField = account is null ? "" : $",\"account\":\"{account}\"";
        return "{\"id\":\"" + id + "\",\"object\":\"event\",\"api_version\":\"2025-09-30.clover\",\"created\":"
            + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"livemode\":false,\"pending_webhooks\":1,\"type\":\"" + type + "\""
            + accountField + ",\"data\":{\"object\":" + dataObject + "}}";
    }

    private static string Refund(string id) => Event(id, "charge.refunded",
        """{"id":"ch_test_1","object":"charge","amount":5000,"amount_refunded":1000,"refunded":false,"payment_intent":"pi_test_1"}""");

    private async Task<List<WebhookFailure>> FailuresAsync(string eventId)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>().Set<WebhookFailure>()
            .AsNoTracking().Where(f => f.EventId == eventId).OrderBy(f => f.Id).ToListAsync();
    }

    private static PostgresException Deadlock() => new("deadlock detected", "ERROR", "ERROR", "40P01");

    [Fact]
    public async Task AnOutage_Answers500_ItsRetriesCountOnOneRow_AndTheFirstSuccessResolvesIt()
    {
        var eventId = NewEventId();
        var payload = Refund(eventId);
        _orders.Failure = Deadlock();

        Assert.Equal(500, await PostAsync(payload));
        Assert.Equal(500, await PostAsync(payload));
        var failing = Assert.Single(await FailuresAsync(eventId));
        Assert.Equal((WebhookFailureKind.HandlerFailed, 2, false), (failing.Kind, failing.Attempts, failing.IsResolved));
        Assert.Equal(payload, failing.Payload);

        _orders.Failure = null;
        Assert.Equal(200, await PostAsync(payload));

        var resolved = Assert.Single(await FailuresAsync(eventId));
        Assert.True(resolved.IsResolved);
        Assert.Equal(new OrderChargeRefundedDto("pi_test_1", "ch_test_1", 5000, 1000, false), _orders.LastRefund);
    }

    [Fact]
    public async Task APermanentFailure_Answers200_IsRecorded_AndLoggedForAlerting()
    {
        var eventId = NewEventId();
        _orders.Failure = new InvalidOperationException("charge.refunded carries no usable amounts");

        Assert.Equal(200, await PostAsync(Refund(eventId)));

        var failure = Assert.Single(await FailuresAsync(eventId));
        Assert.Equal((WebhookFailureKind.HandlerFailed, 1, false), (failure.Kind, failure.Attempts, failure.IsResolved));
        var logged = Assert.Single(_log.Entries, e => e.EventId == StripeWebhookProcessor.FailedLogEvent);
        Assert.Equal(LogLevel.Error, logged.Level);
        Assert.Contains("Kind: Permanent", logged.Message);
        Assert.Contains(eventId, logged.Message);
    }

    [Theory]
    [InlineData("deadlock")]
    [InlineData("timeout")]
    [InlineData("stripe5xx")]
    [InlineData("wrapped")]
    public void Outages_AreTransient(string kind)
    {
        Exception exception = kind switch
        {
            "deadlock" => Deadlock(),
            "timeout" => new TimeoutException(),
            "stripe5xx" => new Stripe.StripeException(System.Net.HttpStatusCode.BadGateway, null!, "bad gateway"),
            _ => new DbUpdateException("save failed", Deadlock()),
        };

        Assert.True(WebhookFailureClassifier.IsTransient(exception));
    }

    [Fact]
    public void BugsAndBadData_ArePermanent()
    {
        Assert.False(WebhookFailureClassifier.IsTransient(new InvalidOperationException("no order")));
        Assert.False(WebhookFailureClassifier.IsTransient(new NullReferenceException()));
        Assert.False(WebhookFailureClassifier.IsTransient(new PostgresException("check violated", "ERROR", "ERROR", "23514")));
        Assert.False(WebhookFailureClassifier.IsTransient(new Stripe.StripeException(System.Net.HttpStatusCode.BadRequest, null!, "bad")));
    }

    [Fact]
    public async Task RecordingAFailure_NeverSavesWhatTheFailedHandlerLeftInTheRequestsDbContext()
    {
        var eventId = NewEventId();
        var leftoverEventId = NewEventId();
        var requestDb = _requestScope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>();
        _orders.BeforeFailing = () => requestDb.Set<WebhookFailure>().Add(WebhookFailure.Create(
            "stripe", leftoverEventId, "leftover", "{}", "half-done work", DateTimeOffset.UtcNow, WebhookFailureKind.HandlerFailed));
        _orders.Failure = new InvalidOperationException("boom");

        await PostAsync(Refund(eventId));

        Assert.Single(await FailuresAsync(eventId));
        Assert.Empty(await FailuresAsync(leftoverEventId));
    }

    [Fact]
    public async Task AnAdmin_ReplaysAFixedFailure_AndResolvesAManualOne()
    {
        var eventId = NewEventId();
        _orders.Failure = new InvalidOperationException("a bug, fixed since");
        await PostAsync(Refund(eventId));
        var failure = Assert.Single(await FailuresAsync(eventId));

        var manualEventId = NewEventId();
        await using (var scope = _services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IWebhookFailureRepository>();
            await repository.AddAsync(WebhookFailure.Create("stripe", manualEventId, "checkout.session.completed",
                "CreatorId=1, SubscriptionId=2", "Refund the charge.", DateTimeOffset.UtcNow, WebhookFailureKind.NeedsAttention), default);
            await repository.SaveChangesAsync(default);
        }
        var manual = Assert.Single(await FailuresAsync(manualEventId));

        await using var adminScope = _services.CreateAsyncScope();
        var admin = new WebhookFailureAdminService(
            adminScope.ServiceProvider.GetRequiredService<IWebhookFailureRepository>(), _stripe, Processor());

        var open = await admin.ListAsync("open", 200, default);
        Assert.Contains(open, f => f.Id == failure.Id && f.Replayable && f.Error == "System.InvalidOperationException: a bug, fixed since");
        Assert.Contains(open, f => f.Id == manual.Id && !f.Replayable);

        await Assert.ThrowsAsync<ConflictException>(() => admin.ReplayAsync(manual.Id, default));

        _orders.Failure = new InvalidOperationException("still broken");
        await Assert.ThrowsAsync<ConflictException>(() => admin.ReplayAsync(failure.Id, default));
        Assert.Equal(2, Assert.Single(await FailuresAsync(eventId)).Attempts);

        _orders.Failure = null;
        await using (var replayScope = _services.CreateAsyncScope())
        {
            var replayAdmin = new WebhookFailureAdminService(
                replayScope.ServiceProvider.GetRequiredService<IWebhookFailureRepository>(), _stripe, Processor());
            var replayed = await replayAdmin.ReplayAsync(failure.Id, default);
            Assert.True(replayed.IsResolved);
            await Assert.ThrowsAsync<ConflictException>(() => replayAdmin.ReplayAsync(failure.Id, default));

            var resolved = await replayAdmin.ResolveAsync(manual.Id, default);
            Assert.True(resolved.IsResolved);
        }

        Assert.True(Assert.Single(await FailuresAsync(eventId)).IsResolved);
        Assert.Equal(1, _orders.RefundCount);
        await Assert.ThrowsAsync<NotFoundException>(() => admin.ReplayAsync(int.MaxValue, default));
        await Assert.ThrowsAsync<BadRequestException>(() => admin.ListAsync("everything", 50, default));
    }

    [Fact]
    public async Task AConnectEvent_GoesToTheAccountHandler_AndAnExpiredSessionToTheOrders()
    {
        var account = Event(NewEventId(), "account.updated",
            """{"id":"acct_test_1","object":"account","details_submitted":true,"charges_enabled":true,"payouts_enabled":true}""",
            account: "acct_test_1");
        var expired = Event(NewEventId(), "checkout.session.expired",
            """{"id":"cs_test_expired","object":"checkout.session","metadata":{"orderId":"8a1f6a51-5a0e-4d0e-9a55-6d1b3f6c2b11","productId":"2"}}""");

        Assert.Equal(200, await PostAsync(account, connect: true));
        Assert.Equal(200, await PostAsync(expired));

        Assert.Equal(("acct_test_1", true), (_creators.LastAccountUpdate!.AccountId, _creators.LastAccountUpdate.PayoutsEnabled));
        Assert.Equal("cs_test_expired", _orders.LastExpired!.SessionId);
        Assert.Equal("8a1f6a51-5a0e-4d0e-9a55-6d1b3f6c2b11", _orders.LastExpired.Metadata![CheckoutSessionMetadata.OrderId]);
    }

    [Fact]
    public async Task ABadSignature_Answers400_AndRecordsNothing()
    {
        var eventId = NewEventId();
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Refund(eventId)));
        context.Request.Headers["Stripe-Signature"] = $"t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()},v1=deadbeef";
        var controller = new StripeWebhooksController(_stripe, Processor(), NullLogger<StripeWebhooksController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.Handle(CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await FailuresAsync(eventId));
        Assert.Equal(0, _orders.RefundCount);
    }

    [Fact]
    public async Task TheMigrationStep_SortsEarlierRowsIntoReplayableAndManual_AndIsIdempotent()
    {
        var jsonEventId = NewEventId();
        var describedEventId = NewEventId();
        const string jsonPayload = "  {\"id\":\"evt\"}";
        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // The state before the migration: no kind, attempts or last failure yet. Rolled back with the transaction.
        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE payments.webhook_failures DROP CONSTRAINT "CK_webhook_failures_Attempts_Positive" """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO payments.webhook_failures ("Provider", "EventId", "EventType", "Payload", "ErrorMessage", "OccurredAt",
                                                   "IsResolved", "Kind", "Attempts", "LastFailedAt")
            VALUES ('stripe', {jsonEventId}, 'charge.refunded', {jsonPayload}, 'boom', '2026-10-01', false, '', 0, '0001-01-01'),
                   ('stripe', {describedEventId}, 'customer.subscription.updated', 'StripeSubscriptionId=sub_1', 'No plan', '2026-10-02',
                    false, '', 0, '0001-01-01')
            """);

        for (var run = 0; run < 2; run++)
            await db.Database.ExecuteSqlRawAsync(TrackWebhookFailureAttempts.BackfillSql);

        var rows = await db.Set<WebhookFailure>().AsNoTracking()
            .Where(f => f.EventId == jsonEventId || f.EventId == describedEventId).OrderBy(f => f.OccurredAt).ToListAsync();
        Assert.Equal(
            [(WebhookFailureKind.HandlerFailed, 1, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)),
             (WebhookFailureKind.NeedsAttention, 1, new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero))],
            rows.Select(f => (f.Kind, f.Attempts, f.LastFailedAt)));

        await transaction.RollbackAsync();
    }

    // ---- B5: one run per event id ----------------------------------------------------------------------------------

    private async Task<DateTimeOffset?> ProcessedAtAsync(string eventId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Set<ProcessedWebhookEvent>().AsNoTracking().Where(e => e.EventId == eventId)
            .Select(e => new { e.ProcessedAt }).ToListAsync()).Single().ProcessedAt;
    }

    private async Task<int> ClaimRowsAsync(string eventId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Set<ProcessedWebhookEvent>().CountAsync(e => e.EventId == eventId);
    }

    [Fact]
    public async Task TheSameEventDeliveredTwice_RunsTheHandlerOnce()
    {
        var eventId = NewEventId();

        Assert.Equal(200, await PostAsync(Refund(eventId)));
        Assert.Equal(200, await PostAsync(Refund(eventId)));

        Assert.Equal(1, _orders.RefundCount);
        Assert.NotNull(await ProcessedAtAsync(eventId));
    }

    [Fact]
    public async Task AConcurrentDelivery_Answers409WhileTheFirstRuns_AndNeverRunsTheHandler()
    {
        var eventId = NewEventId();
        _orders.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = PostAsync(Refund(eventId));
        await _orders.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        int second;
        try
        {
            // Bounded: a second run of the handler would wait on the same gate.
            second = await PostAsync(Refund(eventId)).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _orders.Gate.SetResult();
        }

        Assert.Equal(409, second);
        Assert.Equal(200, await first);
        Assert.Equal(200, await PostAsync(Refund(eventId)));
        Assert.Equal(1, _orders.RefundCount);
    }

    [Fact]
    public async Task AFailedHandler_GivesTheClaimBack_SoTheRetryRuns()
    {
        var eventId = NewEventId();
        _orders.Failure = Deadlock();

        Assert.Equal(500, await PostAsync(Refund(eventId)));
        Assert.Equal(0, await ClaimRowsAsync(eventId));

        _orders.Failure = null;
        Assert.Equal(200, await PostAsync(Refund(eventId)));
        Assert.Equal(1, _orders.RefundCount);
    }

    [Fact]
    public async Task AStaleClaim_FromADeliveryThatDied_IsTakenOver()
    {
        var eventId = NewEventId();
        await using (var db = _fixture.CreateDbContext())
        {
            var claimedAt = DateTimeOffset.UtcNow - StripeWebhookProcessor.ClaimStaleAfter - TimeSpan.FromMinutes(1);
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO payments.processed_webhook_events ("EventId", "Provider", "EventType", "ClaimedAt")
                VALUES ({eventId}, 'stripe', 'charge.refunded', {claimedAt})
                """);
        }

        Assert.Equal(200, await PostAsync(Refund(eventId)));

        Assert.Equal(1, _orders.RefundCount);
        Assert.NotNull(await ProcessedAtAsync(eventId));
    }

    [Fact]
    public async Task ReplayingAnEventALaterDeliveryAlreadyHandled_ResolvesIt_WithoutRunningTheHandler()
    {
        var eventId = NewEventId();
        _orders.Failure = new InvalidOperationException("fixed since");
        await PostAsync(Refund(eventId));
        var failure = Assert.Single(await FailuresAsync(eventId));
        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO payments.processed_webhook_events ("EventId", "Provider", "EventType", "ClaimedAt", "ProcessedAt")
                VALUES ({eventId}, 'stripe', 'charge.refunded', now(), now())
                """);
        _orders.Failure = null;

        await using var scope = _services.CreateAsyncScope();
        var replayed = await new WebhookFailureAdminService(
            scope.ServiceProvider.GetRequiredService<IWebhookFailureRepository>(), _stripe, Processor()).ReplayAsync(failure.Id, default);

        Assert.True(replayed.IsResolved);
        Assert.Equal(0, _orders.RefundCount);
    }

    private sealed class ScriptedOrderWebhooks : IOrderWebhookService
    {
        public Exception? Failure { get; set; }
        public Action? BeforeFailing { get; set; }
        public OrderChargeRefundedDto? LastRefund { get; private set; }
        public OrderCheckoutExpiredDto? LastExpired { get; private set; }
        public int RefundCount { get; private set; }

        /// <summary>When set, a handler call signals <see cref="Entered"/> and waits for it.</summary>
        public TaskCompletionSource? Gate { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task RunAsync()
        {
            if (Gate is not null)
            {
                Entered.TrySetResult();
                await Gate.Task;
            }

            if (Failure is null)
                return;
            BeforeFailing?.Invoke();
            throw Failure;
        }

        public Task HandleCheckoutSessionCompletedAsync(OrderCheckoutCompletedDto data, CancellationToken ct) => RunAsync();

        public async Task HandleCheckoutSessionExpiredAsync(OrderCheckoutExpiredDto data, CancellationToken ct)
        {
            await RunAsync();
            LastExpired = data;
        }

        public async Task HandleChargeRefundedAsync(OrderChargeRefundedDto data, CancellationToken ct)
        {
            await RunAsync();
            LastRefund = data;
            RefundCount++;
        }
    }

    private sealed class RecordingCreatorWebhooks : ICreatorWebhookService
    {
        public AccountUpdatedData? LastAccountUpdate { get; private set; }

        public Task HandleCheckoutSessionCompletedAsync(CheckoutSessionCompletedData data, CancellationToken ct) => Task.CompletedTask;
        public Task HandleSubscriptionCreatedAsync(SubscriptionChangedData data, CancellationToken ct) => Task.CompletedTask;
        public Task HandleSubscriptionUpdatedAsync(SubscriptionChangedData data, CancellationToken ct) => Task.CompletedTask;
        public Task HandleSubscriptionDeletedAsync(SubscriptionChangedData data, CancellationToken ct) => Task.CompletedTask;
        public Task HandleInvoicePaymentFailedAsync(InvoicePaymentFailedData data, CancellationToken ct) => Task.CompletedTask;

        public Task HandleAccountUpdatedAsync(AccountUpdatedData data, CancellationToken ct)
        {
            LastAccountUpdate = data;
            return Task.CompletedTask;
        }
    }

    /// <summary>Captures level, event id and rendered message.</summary>
    private sealed class EventLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, eventId, formatter(state, exception)));
    }
}
