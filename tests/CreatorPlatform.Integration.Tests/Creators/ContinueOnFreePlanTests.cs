using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Creators;

[Collection(PostgresCollection.Name)]
public sealed class ContinueOnFreePlanTests
{
    private const string SessionId = "cs_test_pending_checkout";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly FakeCheckoutSessionService _checkout = new();
    private readonly ListLogger<CreatorService> _logger = new();

    public ContinueOnFreePlanTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private static CreatorService BuildCreatorService(
        CreatorPlatformDbContext db, FakeCheckoutSessionService checkout, ILogger<CreatorService> logger) => new(
        new CreatorRepository(db),
        new CreatorMemberRepository(db),
        new CreatorPlanRepository(db),
        new CreatorSettingsRepository(db),
        new CreatorSubscriptionRepository(db),
        new CreatorPayoutProfileRepository(db),
        new CreatorsUnitOfWork(db),
        checkout,
        new UnexpectedSubscriptionCancellationService(),
        new UnexpectedBillingPortalService(),
        new CreatorOpenBalanceCheck(db),
        logger);

    /// <summary>The production service over the real repositories, on its own context (a request scope).</summary>
    private async Task<CreatorPlatform.Creators.Application.Dtos.CreatorResponseDto> ContinueOnFreeAsync(
        TestData.SeededCreator creator)
    {
        await using var db = _fixture.CreateDbContext();
        return await BuildCreatorService(db, _checkout, _logger).ContinueOnFreePlanAsync(creator.OwnerUserId, CancellationToken.None);
    }

    private async Task AssertUnchangedPendingAsync(TestData.SeededCreator creator, string? checkoutSessionId)
    {
        Assert.Equal("PendingPayment", await _data.GetCreatorStatusAsync(creator.CreatorId));
        var subscription = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.Equal(("basic", "PendingPayment", checkoutSessionId), (subscription.PlanCode, subscription.Status, subscription.CheckoutSessionId));
        Assert.Null(subscription.CancelledAt);
    }

    [Fact]
    public async Task ContinueFree_PendingWorkspace_CancelsPendingActivatesFreeAndExpiresTheSession()
    {
        var creator = await _data.CreatePendingCreatorAsync(SessionId);

        var response = await ContinueOnFreeAsync(creator);

        Assert.Equal(("free", "Active"), (response.PlanCode, response.Status));
        Assert.Equal([SessionId], _checkout.ExpireCalls);
        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));

        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal(2, subscriptions.Count);
        Assert.Equal(("basic", "Cancelled"), (subscriptions[0].PlanCode, subscriptions[0].Status));
        Assert.NotNull(subscriptions[0].CancelledAt);
        Assert.Equal(("free", "Active", "Internal"), (subscriptions[1].PlanCode, subscriptions[1].Status, subscriptions[1].Provider));

        // The workspace's current plan is now Free.
        await using var db = _fixture.CreateDbContext();
        var current = await BuildCreatorService(db, _checkout, NullLogger<CreatorService>.Instance)
            .GetCurrentForOwnerAsync(creator.OwnerUserId, CancellationToken.None);
        Assert.Equal(("free", "Active"), (current!.PlanCode, current.Status));
    }

    [Fact]
    public async Task ContinueFree_SessionAlreadyExpired_StillSwitches()
    {
        var creator = await _data.CreatePendingCreatorAsync(SessionId);
        _checkout.ExpireOutcome = CheckoutSessionExpireOutcome.AlreadyExpired;

        var response = await ContinueOnFreeAsync(creator);

        Assert.Equal(("free", "Active"), (response.PlanCode, response.Status));
        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));
    }

    [Fact]
    public async Task ContinueFree_WorkspaceNotPending_Returns409AndChangesNothing()
    {
        var creator = await _data.CreateCreatorAsync(); // Active, on Free

        await Assert.ThrowsAsync<ConflictException>(() => ContinueOnFreeAsync(creator));

        Assert.Empty(_checkout.ExpireCalls);
        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));
        var subscription = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.Equal(("free", "Active"), (subscription.PlanCode, subscription.Status));
    }

    [Fact]
    public async Task ContinueFree_CustomerAlreadyPaid_Returns409AndChangesNothing()
    {
        var creator = await _data.CreatePendingCreatorAsync(SessionId);
        _checkout.ExpireOutcome = CheckoutSessionExpireOutcome.AlreadyCompleted;

        var exception = await Assert.ThrowsAsync<ConflictException>(() => ContinueOnFreeAsync(creator));

        Assert.Equal("Payment already completed.", exception.Message);
        await AssertUnchangedPendingAsync(creator, SessionId);
    }

    [Fact]
    public async Task ContinueFree_StripeUnreachable_ThrowsAndChangesNothing()
    {
        var creator = await _data.CreatePendingCreatorAsync(SessionId);
        _checkout.ExpireFailure = new InternalServerException("The payment provider could not be reached.");

        await Assert.ThrowsAsync<InternalServerException>(() => ContinueOnFreeAsync(creator));

        await AssertUnchangedPendingAsync(creator, SessionId);
    }

    [Fact]
    public async Task ContinueFree_NoStoredSession_SwitchesAndLogsAWarningWithTheCreatorId()
    {
        var creator = await _data.CreatePendingCreatorAsync(checkoutSessionId: null);

        var response = await ContinueOnFreeAsync(creator);

        Assert.Equal(("free", "Active"), (response.PlanCode, response.Status));
        Assert.Empty(_checkout.ExpireCalls);
        Assert.Contains(_logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains($"CreatorId: {creator.CreatorId}"));
    }

    [Fact]
    public async Task LateCompletedCheckout_ForTheCancelledPendingSubscription_IsNotActivatedAndIsRecordedAsAFailure()
    {
        var creator = await _data.CreatePendingCreatorAsync(checkoutSessionId: null);
        var pendingSubscriptionId = (await _data.GetSubscriptionsAsync(creator.CreatorId)).Single().Id;
        await ContinueOnFreeAsync(creator);
        var eventId = $"evt_{Guid.NewGuid():N}";

        await using (var db = _fixture.CreateDbContext())
        {
            var webhooks = new CreatorWebhookService(
                new CreatorRepository(db),
                new CreatorSubscriptionRepository(db),
                new CreatorPlanRepository(db),
                new WebhookFailureRepository(db),
                new CreatorsUnitOfWork(db),
                new FakeBillingPeriodService(),
                NullLogger<CreatorWebhookService>.Instance);

            await webhooks.HandleCheckoutSessionCompletedAsync(new CheckoutSessionCompletedData
            {
                EventId = eventId,
                SessionId = "cs_test_paid_late",
                StripeSubscriptionId = $"sub_{Guid.NewGuid():N}",
                StripeCustomerId = "cus_test_late",
                Metadata = new Dictionary<string, string>
                {
                    ["creatorId"] = creator.CreatorId.ToString(),
                    ["subscriptionId"] = pendingSubscriptionId.ToString(),
                    ["planCode"] = "basic",
                },
            }, CancellationToken.None);
        }

        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal(("Cancelled", null), (subscriptions[0].Status, subscriptions[0].ProviderSubscriptionId));
        Assert.Equal(("free", "Active"), (subscriptions[1].PlanCode, subscriptions[1].Status));
        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));
        var failure = Assert.Single(await _data.GetWebhookFailureMessagesAsync(eventId));
        Assert.Contains("refund", failure);
    }

    [Fact]
    public async Task StartCheckout_StoresTheSessionIdAndAReCheckoutReplacesIt()
    {
        var creator = await _data.CreatePendingCreatorAsync(checkoutSessionId: null);

        foreach (var sessionId in new[] { "cs_test_first", "cs_test_second" })
        {
            _checkout.NextSessionId = sessionId;
            await using var db = _fixture.CreateDbContext();
            await BuildCreatorService(db, _checkout, NullLogger<CreatorService>.Instance)
                .StartSubscriptionCheckoutAsync(creator.OwnerUserId, CancellationToken.None);
        }

        var subscription = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.Equal("cs_test_second", subscription.CheckoutSessionId);
    }
}
