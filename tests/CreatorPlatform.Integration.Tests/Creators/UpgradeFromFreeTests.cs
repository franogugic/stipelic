using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using LandingPagesContextProvider = CreatorPlatform.LandingPages.Infrastructure.Services.CreatorContextProvider;
using ProductsContextProvider = CreatorPlatform.Products.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Creators;

/// <summary>Free → paid: a pending subscription waits next to the still-active Free one until Checkout is paid; then
/// the paid plan takes over and Free is retired, leaving exactly one current subscription.</summary>
[Collection(PostgresCollection.Name)]
public sealed class UpgradeFromFreeTests
{
    private const string OwnerEmail = "owner@example.test";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly FakeCheckoutSessionService _checkout = new();
    private readonly FakeBillingCustomerService _customers = new();
    private readonly RecordingCreatorCacheInvalidator _invalidator = new();

    public UpgradeFromFreeTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    // ---- Happy path ----------------------------------------------------------------------------------------

    [Fact]
    public async Task FreeToPaid_PendingWhileUnpaid_ThenPaidActiveAndFreeCancelled()
    {
        var creator = await _data.CreateCreatorAsync();

        var response = await UpgradeAsync(creator, "basic");

        Assert.Equal((true, "PendingPayment"), (response.RequiresPayment, response.PaymentStatus));
        Assert.Equal(_checkout.CreateCalls[0].SessionId, response.CheckoutUrl!.Split('/')[^1]);

        // A Stripe customer was created once, stored, and is what Checkout bills.
        var customerId = await StripeCustomerIdAsync(creator.CreatorId);
        Assert.NotNull(customerId);
        Assert.Equal([OwnerEmail], _customers.CreatedFor);
        Assert.Equal(customerId, _checkout.CreateCalls[0].CustomerId);

        // Until it is paid the workspace stays on Free.
        Assert.Equal(["Active", "PendingPayment"], (await _data.GetSubscriptionsAsync(creator.CreatorId)).Select(s => s.Status));
        var unpaid = await GetCurrentAsync(creator);
        Assert.Equal(("free", "Active", "Active"), (unpaid.PlanCode, unpaid.SubscriptionStatus, unpaid.Status));

        await CompleteAsync(_checkout.CreateCalls[0], "sub_test_upgrade_" + creator.CreatorId, customerId!);

        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal(("free", "Cancelled"), (subscriptions[0].PlanCode, subscriptions[0].Status));
        Assert.Equal(("basic", "Active", "Stripe"), (subscriptions[1].PlanCode, subscriptions[1].Status, subscriptions[1].Provider));
        Assert.Single(subscriptions, s => s.Status != "Cancelled");
        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));

        var paid = await GetCurrentAsync(creator);
        Assert.Equal(("basic", "Active"), (paid.PlanCode, paid.SubscriptionStatus));
        Assert.Equal([creator.CreatorId], _invalidator.Invalidated);
    }

    [Fact]
    public async Task AnAbandonedUpgrade_KeepsFreeWorking_WithTheFreeLimits()
    {
        var creator = await _data.CreateCreatorAsync();

        await UpgradeAsync(creator, "basic");

        var current = await GetCurrentAsync(creator);
        Assert.Equal(("free", "Active"), (current.PlanCode, current.SubscriptionStatus));

        // The pending (paid) plan must not lift the limits before it is paid.
        await using var db = _fixture.CreateDbContext();
        var pages = await new LandingPagesContextProvider(db).GetBySlugForOwnerAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None);
        var products = await new ProductsContextProvider(db).GetBySlugForOwnerAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None);
        Assert.Equal(1, pages!.MaxLandingPages);
        Assert.Equal(1, products!.MaxProducts);
    }

    [Fact]
    public async Task ASecondAttempt_ExpiresTheFirstSession_AndReplacesIt()
    {
        var creator = await _data.CreateCreatorAsync();
        await UpgradeAsync(creator, "basic");
        var first = _checkout.CreateCalls[0];

        await UpgradeAsync(creator, "pro");

        Assert.Equal([first.SessionId], _checkout.ExpireCalls);
        Assert.Single(_customers.CreatedFor);                       // the stored customer is reused
        Assert.Equal(_checkout.CreateCalls[0].CustomerId, _checkout.CreateCalls[1].CustomerId);
        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal(
            [("free", "Active"), ("basic", "Cancelled"), ("pro", "PendingPayment")],
            subscriptions.Select(s => (s.PlanCode, s.Status)));

        // The replaced attempt can no longer activate anything, even if it were paid.
        await CompleteAsync(first, "sub_test_late_" + creator.CreatorId, (await StripeCustomerIdAsync(creator.CreatorId))!);
        Assert.Equal(("free", "Active"), ((await GetCurrentAsync(creator)).PlanCode, (await GetCurrentAsync(creator)).SubscriptionStatus));
    }

    [Fact]
    public async Task AnUpgradePaidMeanwhile_Is409OnTheNextAttempt()
    {
        var creator = await _data.CreateCreatorAsync();
        await UpgradeAsync(creator, "basic");
        _checkout.ExpireOutcome = CheckoutSessionExpireOutcome.AlreadyCompleted;

        var exception = await Assert.ThrowsAsync<ConflictException>(() => UpgradeAsync(creator, "pro"));

        Assert.Equal("Payment already completed.", exception.Message);
        Assert.Equal(["Active", "PendingPayment"], (await _data.GetSubscriptionsAsync(creator.CreatorId)).Select(s => s.Status));
    }

    [Fact]
    public async Task AReplayedCheckoutCompleted_IsIdempotent()
    {
        var creator = await _data.CreateCreatorAsync();
        await UpgradeAsync(creator, "basic");
        var customerId = (await StripeCustomerIdAsync(creator.CreatorId))!;
        var stripeSubscriptionId = "sub_test_replay_" + creator.CreatorId;

        await CompleteAsync(_checkout.CreateCalls[0], stripeSubscriptionId, customerId);
        await CompleteAsync(_checkout.CreateCalls[0], stripeSubscriptionId, customerId);

        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal([("free", "Cancelled"), ("basic", "Active")], subscriptions.Select(s => (s.PlanCode, s.Status)));
    }

    [Fact]
    public async Task TwoInterleavedStarts_TheSecondGets409_AndTheFirstSessionStaysTheOnlyPayableOne()
    {
        var creator = await _data.CreateCreatorAsync();
        // Both requests pass this point (after reading "no earlier attempt", before the lock) before either goes on.
        var barrier = new BarrierCustomerService(parties: 2);

        var attempts = await Task.WhenAll(
            CaptureAsync(() => WithServiceAsync(s => s.StartSubscriptionCheckoutAsync(creator.OwnerUserId, OwnerEmail, "basic", CancellationToken.None), barrier)),
            CaptureAsync(() => WithServiceAsync(s => s.StartSubscriptionCheckoutAsync(creator.OwnerUserId, OwnerEmail, "pro", CancellationToken.None), barrier)));

        var conflict = Assert.IsType<ConflictException>(Assert.Single(attempts, a => a.Error is not null).Error);
        Assert.Equal("upgrade_in_progress", conflict.Code);
        Assert.Equal(
            "An upgrade is already starting. Use the checkout page that opened, or try again in a minute.",
            conflict.Message);

        // The winner's Checkout is the only one, and its pending row is still payable (nothing cancelled, nothing expired).
        var session = Assert.Single(_checkout.CreateCalls);
        Assert.Empty(_checkout.ExpireCalls);
        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal(["Active", "PendingPayment"], subscriptions.Select(s => s.Status));
        Assert.Equal(session.SessionId, subscriptions[1].CheckoutSessionId);
        Assert.Equal(session.Metadata["subscriptionId"], subscriptions[1].Id.ToString());
    }

    private static async Task<(StartCreatorSubscriptionCheckoutResponseDto? Response, Exception? Error)> CaptureAsync(
        Func<Task<StartCreatorSubscriptionCheckoutResponseDto>> start)
    {
        try
        {
            return (await start(), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    /// <summary>Creates the customer only after <c>parties</c> callers have arrived — pins two requests at the same
    /// point of the flow.</summary>
    private sealed class BarrierCustomerService(int parties) : IBillingCustomerService
    {
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public async Task<string> CreateAsync(
            string email, string name, IReadOnlyDictionary<string, string> metadata, string idempotencyKey, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _arrived) == parties)
                _allArrived.SetResult();
            await _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            // Stripe's idempotency key returns the same customer to both.
            return $"cus_test_{idempotencyKey}";
        }
    }

    // ---- Rejections ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("free")]
    [InlineData("does-not-exist")]
    [InlineData(null)]
    public async Task TheFreePlanAnUnknownPlanOrNoPlan_Is400(string? planCode)
    {
        var creator = await _data.CreateCreatorAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => UpgradeAsync(creator, planCode));

        Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.Empty(_customers.CreatedFor);
        Assert.Empty(_checkout.CreateCalls);
    }

    [Fact]
    public async Task AWorkspaceOnAPaidPlan_Is409()
    {
        var creator = await _data.CreateCreatorAsync(TestData.BasicPlanId);

        await Assert.ThrowsAsync<ConflictException>(() => UpgradeAsync(creator, "pro"));

        Assert.Empty(_checkout.CreateCalls);
    }

    [Fact]
    public async Task AWorkspaceThatIsNotActive_Is409()
    {
        var creator = await _data.CreateCreatorAsync();
        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""UPDATE creators.creators SET "Status" = 'Suspended' WHERE "Id" = {creator.CreatorId}""");

        await Assert.ThrowsAsync<ConflictException>(() => UpgradeAsync(creator, "basic"));
    }

    // ---- Deleting a workspace with a pending upgrade (B12.1) -----------------------------------------------

    [Fact]
    public async Task DeletingTheWorkspace_ExpiresAndCancelsThePendingUpgrade()
    {
        var creator = await _data.CreateCreatorAsync();
        await UpgradeAsync(creator, "basic");

        await WithServiceAsync(s => s.DeleteCurrentAsync(creator.OwnerUserId, CancellationToken.None));

        Assert.Equal([_checkout.CreateCalls[0].SessionId], _checkout.ExpireCalls);
        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
        Assert.All(await _data.GetSubscriptionsAsync(creator.CreatorId), s => Assert.Equal("Cancelled", s.Status));
    }

    // ---- Helpers -------------------------------------------------------------------------------------------

    private Task<StartCreatorSubscriptionCheckoutResponseDto> UpgradeAsync(TestData.SeededCreator creator, string? planCode) =>
        WithServiceAsync(s => s.StartSubscriptionCheckoutAsync(creator.OwnerUserId, OwnerEmail, planCode, CancellationToken.None));

    private Task<CreatorResponseDto> GetCurrentAsync(TestData.SeededCreator creator) =>
        WithServiceAsync(async s => (await s.GetCurrentForOwnerAsync(creator.OwnerUserId, CancellationToken.None))!);

    private async Task<T> WithServiceAsync<T>(Func<CreatorService, Task<T>> act, IBillingCustomerService? customers = null)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(new CreatorService(
            new CreatorRepository(db),
            new CreatorMemberRepository(db),
            new CreatorPlanRepository(db),
            new CreatorSettingsRepository(db),
            new CreatorSubscriptionRepository(db),
            new CreatorPayoutProfileRepository(db),
            new CreatorsUnitOfWork(db),
            _checkout,
            new UnexpectedSubscriptionCancellationService(),
            new UnexpectedBillingPortalService(),
            new CreatorOpenBalanceCheck(db),
            customers ?? _customers,
            NullLogger<CreatorService>.Instance));
    }

    private async Task CompleteAsync(FakeCheckoutSessionService.CreateCall session, string stripeSubscriptionId, string customerId)
    {
        await using var db = _fixture.CreateDbContext();
        var webhooks = new CreatorWebhookService(
            new CreatorRepository(db),
            new CreatorSubscriptionRepository(db),
            new CreatorPlanRepository(db),
            new WebhookFailureRepository(db),
            new CreatorsUnitOfWork(db),
            new FakeBillingPeriodService(),
            _invalidator,
            NullLogger<CreatorWebhookService>.Instance);

        await webhooks.HandleCheckoutSessionCompletedAsync(new CheckoutSessionCompletedData
        {
            EventId = $"evt_{Guid.NewGuid():N}",
            SessionId = session.SessionId,
            StripeSubscriptionId = stripeSubscriptionId,
            StripeCustomerId = customerId,
            Metadata = session.Metadata,
        }, CancellationToken.None);
    }

    private async Task<string?> StripeCustomerIdAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<string?>($"""
            SELECT "StripeCustomerId" AS "Value" FROM creators.creators WHERE "Id" = {creatorId}
            """).ToListAsync()).Single();
    }
}
