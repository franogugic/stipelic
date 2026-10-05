using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Creators;

/// <summary>When a paid Stripe subscription ends (customer.subscription.deleted, or .updated with status canceled —
/// Stripe sends both), the workspace moves to an Active Free subscription and keeps its content.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PaidPlanEndsTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly RecordingCreatorCacheInvalidator _invalidator = new();

    // xUnit creates the class per test, so every test owns its Stripe subscription id (the DB is shared).
    private readonly string _stripeSubscriptionId = $"sub_test_{Guid.NewGuid():N}";
    private readonly DateTimeOffset _periodEnd = DateTimeOffset.UtcNow.AddDays(9).AddTicks(-(DateTimeOffset.UtcNow.Ticks % 10));

    public PaidPlanEndsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    // ---- Moving to Free ------------------------------------------------------------------------------------

    [Fact]
    public async Task SubscriptionDeleted_MovesTheWorkspaceToAnActiveFreePlan_AndKeepsItsContent()
    {
        var creator = await CreatePaidCreatorAsync(cancelAtPeriodEnd: true);
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var productId = await CreateProductAsync(creator.CreatorId);

        // Until the event arrives, the shell keeps showing "ends on {date}".
        var before = await GetCurrentAsync(creator);
        Assert.Equal(("basic", "Active", true, _periodEnd), (before.PlanCode, before.SubscriptionStatus, before.CancelAtPeriodEnd, before.CurrentPeriodEnd));

        await DeletedAsync(Event("canceled"));

        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));
        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal(2, subscriptions.Count);
        Assert.Equal(("basic", "Cancelled"), (subscriptions[0].PlanCode, subscriptions[0].Status));
        Assert.NotNull(subscriptions[0].CancelledAt);
        Assert.Equal(("free", "Active", "Internal"), (subscriptions[1].PlanCode, subscriptions[1].Status, subscriptions[1].Provider));

        var after = await GetCurrentAsync(creator);
        Assert.Equal(("free", "Active", false, (DateTimeOffset?)null), (after.PlanCode, after.SubscriptionStatus, after.CancelAtPeriodEnd, after.CurrentPeriodEnd));

        // Content stays exactly as it was.
        Assert.Equal("Published", await ScalarStringAsync($"""SELECT "Status" AS "Value" FROM landing_pages.landing_pages WHERE "Id" = {page}"""));
        Assert.Equal("Active", await ScalarStringAsync($"""SELECT "Status" AS "Value" FROM products.products WHERE "Id" = {productId}"""));

        Assert.Equal([creator.CreatorId], _invalidator.Invalidated);
    }

    [Fact]
    public async Task SubscriptionUpdatedToCanceled_AlsoMovesToFree_AndTheFollowingDeletedIsANoOp()
    {
        var creator = await CreatePaidCreatorAsync(cancelAtPeriodEnd: false);

        await UpdatedAsync(Event("canceled"));
        await DeletedAsync(Event("canceled", secondsLater: 1));

        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));
        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal(["Cancelled", "Active"], subscriptions.Select(s => s.Status));
        Assert.Equal("free", subscriptions[1].PlanCode);
    }

    [Fact]
    public async Task AReplayedDeletedEvent_CreatesNoSecondFreeSubscription()
    {
        var creator = await CreatePaidCreatorAsync(cancelAtPeriodEnd: true);
        var deleted = Event("canceled");

        await DeletedAsync(deleted);
        await DeletedAsync(deleted);

        Assert.Equal(2, (await _data.GetSubscriptionsAsync(creator.CreatorId)).Count);
    }

    [Fact]
    public async Task DeletedAndUpdatedAtTheSameTime_CreateExactlyOneFreeSubscription()
    {
        var creator = await CreatePaidCreatorAsync(cancelAtPeriodEnd: true);

        await Task.WhenAll(DeletedAsync(Event("canceled")), UpdatedAsync(Event("canceled")));

        var subscriptions = await _data.GetSubscriptionsAsync(creator.CreatorId);
        Assert.Equal(["Cancelled", "Active"], subscriptions.Select(s => s.Status));
    }

    [Fact]
    public async Task AStaleDeletedEvent_ChangesNothing()
    {
        var creator = await CreatePaidCreatorAsync(cancelAtPeriodEnd: false);
        await ExecuteAsync($"""
            UPDATE creators.creator_subscriptions SET "ProviderEventAt" = now() WHERE "CreatorId" = {creator.CreatorId}
            """);

        await DeletedAsync(Event("canceled", secondsLater: -3600));

        var subscription = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.Equal(("basic", "Active"), (subscription.PlanCode, subscription.Status));
        Assert.Empty(_invalidator.Invalidated);
    }

    // ---- Workspaces that aren't simply Active --------------------------------------------------------------

    [Fact]
    public async Task ADeletedWorkspace_StaysDisabled_AndGetsNoFreePlan()
    {
        var creator = await CreatePaidCreatorAsync(cancelAtPeriodEnd: false);
        await ExecuteAsync($"""UPDATE creators.creators SET "Status" = 'Disabled' WHERE "Id" = {creator.CreatorId}""");

        await DeletedAsync(Event("canceled"));

        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
        var subscription = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.Equal("Cancelled", subscription.Status);
    }

    [Fact]
    public async Task AWorkspaceSuspendedByTheOldBehaviour_ComesBackOnFree()
    {
        var creator = await CreatePaidCreatorAsync(cancelAtPeriodEnd: false);
        await ExecuteAsync($"""UPDATE creators.creators SET "Status" = 'Suspended' WHERE "Id" = {creator.CreatorId}""");

        await DeletedAsync(Event("canceled"));

        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));
        Assert.Equal("free", (await _data.GetSubscriptionsAsync(creator.CreatorId))[1].PlanCode);
    }

    // ---- Helpers -------------------------------------------------------------------------------------------

    private SubscriptionChangedData Event(string status, int secondsLater = 0) => new()
    {
        EventId = $"evt_{Guid.NewGuid():N}",
        StripeSubscriptionId = _stripeSubscriptionId,
        StripeCustomerId = "cus_test_ends",
        Status = status,
        OccurredAt = DateTimeOffset.UtcNow.AddSeconds(secondsLater),
    };

    private Task DeletedAsync(SubscriptionChangedData data) =>
        WithWebhooksAsync(webhooks => webhooks.HandleSubscriptionDeletedAsync(data, CancellationToken.None));

    private Task UpdatedAsync(SubscriptionChangedData data) =>
        WithWebhooksAsync(webhooks => webhooks.HandleSubscriptionUpdatedAsync(data, CancellationToken.None));

    private async Task WithWebhooksAsync(Func<CreatorWebhookService, Task> act)
    {
        await using var db = _fixture.CreateDbContext();
        await act(new CreatorWebhookService(
            new CreatorRepository(db),
            new CreatorSubscriptionRepository(db),
            new CreatorPlanRepository(db),
            new WebhookFailureRepository(db),
            new CreatorsUnitOfWork(db),
            new FakeBillingPeriodService(),
            _invalidator,
            NullLogger<CreatorWebhookService>.Instance));
    }

    private async Task<CreatorResponseDto> GetCurrentAsync(TestData.SeededCreator creator)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new CreatorService(
            new CreatorRepository(db),
            new CreatorMemberRepository(db),
            new CreatorPlanRepository(db),
            new CreatorSettingsRepository(db),
            new CreatorSubscriptionRepository(db),
            new CreatorPayoutProfileRepository(db),
            new CreatorsUnitOfWork(db),
            new FakeCheckoutSessionService(),
            new UnexpectedSubscriptionCancellationService(),
            new UnexpectedBillingPortalService(),
            new CreatorOpenBalanceCheck(db),
            NullLogger<CreatorService>.Instance);
        return (await service.GetCurrentForOwnerAsync(creator.OwnerUserId, CancellationToken.None))!;
    }

    /// <summary>A workspace on the paid Basic plan billed by this test's Stripe subscription.</summary>
    private async Task<TestData.SeededCreator> CreatePaidCreatorAsync(bool cancelAtPeriodEnd)
    {
        var creator = await _data.CreateCreatorAsync(TestData.BasicPlanId);
        var periodStart = _periodEnd.AddMonths(-1);
        await ExecuteAsync($"""
            UPDATE creators.creator_subscriptions
            SET "Provider" = 'Stripe', "ProviderSubscriptionId" = {_stripeSubscriptionId}, "BillingInterval" = 'Monthly',
                "CancelAtPeriodEnd" = {cancelAtPeriodEnd}, "CurrentPeriodStart" = {periodStart}, "CurrentPeriodEnd" = {_periodEnd}
            WHERE "CreatorId" = {creator.CreatorId}
            """);
        return creator;
    }

    private async Task<int> CreateProductAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Kept product', 1000, 'Digital', 'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task ExecuteAsync(FormattableString sql)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync(sql);
    }

    private async Task<string> ScalarStringAsync(FormattableString sql)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<string>(sql).ToListAsync()).Single();
    }
}
