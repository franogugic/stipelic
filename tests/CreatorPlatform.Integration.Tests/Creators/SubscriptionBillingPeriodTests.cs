using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Integration.Tests.Creators;

/// <summary>The billing period and the stale-event guard across checkout.session.completed and
/// customer.subscription.created/updated, on real SQL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SubscriptionBillingPeriodTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly SubscriptionBillingPeriodDto October = new(T0, T0.AddMonths(1));
    private static readonly SubscriptionBillingPeriodDto November = new(T0.AddMonths(1), T0.AddMonths(2));

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly FakeBillingPeriodService _periods = new();
    private readonly ListLogger<CreatorWebhookService> _logger = new();

    public SubscriptionBillingPeriodTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task WithWebhooksAsync(Func<CreatorWebhookService, Task> act)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new CreatorWebhookService(
            new CreatorRepository(db),
            new CreatorSubscriptionRepository(db),
            new CreatorPlanRepository(db),
            new WebhookFailureRepository(db),
            new CreatorsUnitOfWork(db),
            _periods,
            new RecordingCreatorCacheInvalidator(),
            _logger);
        await act(service);
    }

    private static Dictionary<string, string> Metadata(TestData.SeededCreator creator, int subscriptionId) => new()
    {
        ["creatorId"] = creator.CreatorId.ToString(),
        ["subscriptionId"] = subscriptionId.ToString(),
        ["planCode"] = "basic",
    };

    private Task CompleteCheckoutAsync(TestData.SeededCreator creator, int subscriptionId, string stripeSubscriptionId) =>
        WithWebhooksAsync(webhooks => webhooks.HandleCheckoutSessionCompletedAsync(new CheckoutSessionCompletedData
        {
            EventId = $"evt_{Guid.NewGuid():N}",
            SessionId = $"cs_{Guid.NewGuid():N}",
            StripeSubscriptionId = stripeSubscriptionId,
            StripeCustomerId = $"cus_{Guid.NewGuid():N}",
            Metadata = Metadata(creator, subscriptionId),
        }, CancellationToken.None));

    private static SubscriptionChangedData SubscriptionEvent(
        TestData.SeededCreator creator, int subscriptionId, string stripeSubscriptionId,
        SubscriptionBillingPeriodDto period, DateTimeOffset occurredAt, bool cancelAtPeriodEnd = false) => new()
    {
        EventId = $"evt_{Guid.NewGuid():N}",
        StripeSubscriptionId = stripeSubscriptionId,
        StripeCustomerId = $"cus_{Guid.NewGuid():N}",
        Status = "active",
        CancelAtPeriodEnd = cancelAtPeriodEnd,
        CurrentPeriodStart = period.CurrentPeriodStart,
        CurrentPeriodEnd = period.CurrentPeriodEnd,
        Metadata = Metadata(creator, subscriptionId),
        OccurredAt = occurredAt,
    };

    /// <summary>A pending workspace and its pending subscription id.</summary>
    private async Task<(TestData.SeededCreator Creator, int SubscriptionId, string StripeSubscriptionId)> PendingAsync()
    {
        var creator = await _data.CreatePendingCreatorAsync(checkoutSessionId: null);
        var subscriptionId = (await _data.GetSubscriptionsAsync(creator.CreatorId)).Single().Id;
        return (creator, subscriptionId, $"sub_{Guid.NewGuid():N}");
    }

    private async Task<TestData.SubscriptionRow> SubscriptionAsync(TestData.SeededCreator creator)
        => (await _data.GetSubscriptionsAsync(creator.CreatorId)).Single();

    [Fact]
    public async Task CheckoutCompleted_SetsThePeriodFromTheStripeSubscription()
    {
        var (creator, subscriptionId, stripeId) = await PendingAsync();
        _periods.Period = October;

        await CompleteCheckoutAsync(creator, subscriptionId, stripeId);

        var subscription = await SubscriptionAsync(creator);
        Assert.Equal(("Active", stripeId), (subscription.Status, subscription.ProviderSubscriptionId));
        Assert.Equal((October.CurrentPeriodStart, October.CurrentPeriodEnd), (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd));
        Assert.Equal([stripeId], _periods.Reads);
    }

    [Fact]
    public async Task CreatedBeforeCheckout_SetsThePeriodAndCheckoutDoesNotRegressIt()
    {
        var (creator, subscriptionId, stripeId) = await PendingAsync();
        await WithWebhooksAsync(w => w.HandleSubscriptionCreatedAsync(
            SubscriptionEvent(creator, subscriptionId, stripeId, November, T0), CancellationToken.None));
        Assert.Equal(November.CurrentPeriodEnd, (await SubscriptionAsync(creator)).CurrentPeriodEnd);

        // The checkout's own read returns an older period: the stored one stays.
        _periods.Period = October;
        await CompleteCheckoutAsync(creator, subscriptionId, stripeId);

        var subscription = await SubscriptionAsync(creator);
        Assert.Equal("Active", subscription.Status);
        Assert.Equal((November.CurrentPeriodStart, November.CurrentPeriodEnd), (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd));
    }

    [Fact]
    public async Task CheckoutCompleted_StripeReadFails_ActivatesWithANullPeriodAndLogsAWarning()
    {
        var (creator, subscriptionId, stripeId) = await PendingAsync();
        _periods.Failure = new HttpRequestException("Stripe is unreachable.");

        await CompleteCheckoutAsync(creator, subscriptionId, stripeId);

        var subscription = await SubscriptionAsync(creator);
        Assert.Equal(("Active", stripeId), (subscription.Status, subscription.ProviderSubscriptionId));
        Assert.Null(subscription.CurrentPeriodStart);
        Assert.Null(subscription.CurrentPeriodEnd);
        Assert.Equal("Active", await _data.GetCreatorStatusAsync(creator.CreatorId));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(stripeId));
    }

    [Fact]
    public async Task OlderUpdatedAfterANewerOne_IsIgnored()
    {
        var (creator, subscriptionId, stripeId) = await PendingAsync();
        _periods.Period = October;
        await CompleteCheckoutAsync(creator, subscriptionId, stripeId);
        await WithWebhooksAsync(w => w.HandleSubscriptionUpdatedAsync(
            SubscriptionEvent(creator, subscriptionId, stripeId, November, T0.AddMinutes(10)), CancellationToken.None));

        // Delivered late: happened before the event already applied, and would set CancelAtPeriodEnd + October.
        await WithWebhooksAsync(w => w.HandleSubscriptionUpdatedAsync(
            SubscriptionEvent(creator, subscriptionId, stripeId, October, T0.AddMinutes(5), cancelAtPeriodEnd: true),
            CancellationToken.None));

        var subscription = await SubscriptionAsync(creator);
        Assert.False(subscription.CancelAtPeriodEnd);
        Assert.Equal(November.CurrentPeriodEnd, subscription.CurrentPeriodEnd);
        Assert.Equal(T0.AddMinutes(10), subscription.ProviderEventAt);
        Assert.Contains(_logger.Entries, e => e.Message.Contains("Ignoring stale"));
    }

    [Fact]
    public async Task NewerEventWithAnOlderPeriod_AppliesButNeverMovesThePeriodBackwards()
    {
        var (creator, subscriptionId, stripeId) = await PendingAsync();
        _periods.Period = November;
        await CompleteCheckoutAsync(creator, subscriptionId, stripeId);

        await WithWebhooksAsync(w => w.HandleSubscriptionUpdatedAsync(
            SubscriptionEvent(creator, subscriptionId, stripeId, October, T0.AddMinutes(1), cancelAtPeriodEnd: true),
            CancellationToken.None));

        var subscription = await SubscriptionAsync(creator);
        Assert.True(subscription.CancelAtPeriodEnd);
        Assert.Equal(November.CurrentPeriodEnd, subscription.CurrentPeriodEnd);
    }

    [Fact]
    public async Task CreatedOnAPendingSubscription_SetsThePeriodButDoesNotActivateOrLink()
    {
        var (creator, subscriptionId, stripeId) = await PendingAsync();

        await WithWebhooksAsync(w => w.HandleSubscriptionCreatedAsync(
            SubscriptionEvent(creator, subscriptionId, stripeId, October, T0), CancellationToken.None));

        var subscription = await SubscriptionAsync(creator);
        Assert.Equal(("PendingPayment", null), (subscription.Status, subscription.ProviderSubscriptionId));
        Assert.Equal((October.CurrentPeriodStart, October.CurrentPeriodEnd), (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd));
        Assert.Equal(T0, subscription.ProviderEventAt);
        Assert.Equal("PendingPayment", await _data.GetCreatorStatusAsync(creator.CreatorId));
    }

    [Fact]
    public async Task CreatedOnACancelledSubscription_IsIgnored()
    {
        var (creator, subscriptionId, stripeId) = await PendingAsync();
        await using (var db = _fixture.CreateDbContext())
        {
            // The state continue-free leaves behind.
            await db.Database.ExecuteSqlAsync($"""
                UPDATE creators.creator_subscriptions SET "Status" = 'Cancelled', "CancelledAt" = now()
                WHERE "Id" = {subscriptionId}
                """);
        }
        var createdEvent = SubscriptionEvent(creator, subscriptionId, stripeId, October, T0);

        await WithWebhooksAsync(w => w.HandleSubscriptionCreatedAsync(createdEvent, CancellationToken.None));

        var subscription = await SubscriptionAsync(creator);
        Assert.Equal(("Cancelled", null), (subscription.Status, subscription.ProviderSubscriptionId));
        Assert.Null(subscription.CurrentPeriodEnd);
        Assert.Null(subscription.ProviderEventAt);
        Assert.Empty(await _data.GetWebhookFailureMessagesAsync(createdEvent.EventId));
    }
}
