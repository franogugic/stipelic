using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.MoneyPath.Tests.Creators;

public class CreatorWebhookServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreatorWebhookService BuildService(FakeCreatorRepository creatorRepository, FakeCreatorsUnitOfWork unitOfWork)
    {
        return BuildService(
            creatorRepository,
            new FakeCreatorSubscriptionRepository(),
            new FakeCreatorPlanRepository(),
            new FakeWebhookFailureRepository(),
            unitOfWork);
    }

    private static CreatorWebhookService BuildService(
        FakeCreatorRepository creatorRepository,
        FakeCreatorSubscriptionRepository subscriptionRepository,
        FakeCreatorPlanRepository planRepository,
        FakeWebhookFailureRepository webhookFailureRepository,
        FakeCreatorsUnitOfWork unitOfWork)
    {
        return new CreatorWebhookService(
            creatorRepository,
            subscriptionRepository,
            planRepository,
            webhookFailureRepository,
            unitOfWork,
            NullLogger<CreatorWebhookService>.Instance);
    }

    [Fact]
    public async Task HandleAccountUpdated_KnownAccount_SetsAllThreeFlags()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        creator.SetStripeConnectAccountId("acct_123", Now);
        var repo = new FakeCreatorRepository { CreatorByStripeConnectAccountId = creator };
        var uow = new FakeCreatorsUnitOfWork();
        var service = BuildService(repo, uow);

        var data = new AccountUpdatedData
        {
            AccountId = "acct_123",
            DetailsSubmitted = true,
            ChargesEnabled = true,
            PayoutsEnabled = true
        };

        await service.HandleAccountUpdatedAsync(data, CancellationToken.None);

        Assert.True(creator.StripeConnectDetailsSubmitted);
        Assert.True(creator.StripeConnectChargesEnabled);
        Assert.True(creator.StripeConnectPayoutsEnabled);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleAccountUpdated_StaleEventAfterNewer_IsNoOp()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        creator.SetStripeConnectAccountId("acct_123", Now);
        var repo = new FakeCreatorRepository { CreatorByStripeConnectAccountId = creator };
        var uow = new FakeCreatorsUnitOfWork();
        var service = BuildService(repo, uow);

        var newerEvent = new AccountUpdatedData
        {
            AccountId = "acct_123",
            DetailsSubmitted = true,
            ChargesEnabled = true,
            PayoutsEnabled = true,
            OccurredAt = Now,
        };
        await service.HandleAccountUpdatedAsync(newerEvent, CancellationToken.None);

        // A delayed/replayed event reporting an earlier (falsy) state, timestamped before the one
        // already applied — must not overwrite the newer, already-applied state.
        var staleEvent = new AccountUpdatedData
        {
            AccountId = "acct_123",
            DetailsSubmitted = false,
            ChargesEnabled = false,
            PayoutsEnabled = false,
            OccurredAt = Now.AddHours(-1),
        };
        await service.HandleAccountUpdatedAsync(staleEvent, CancellationToken.None);

        Assert.True(creator.StripeConnectDetailsSubmitted);
        Assert.True(creator.StripeConnectChargesEnabled);
        Assert.True(creator.StripeConnectPayoutsEnabled);
        Assert.Equal(Now, creator.StripeConnectStatusEventAt);
    }

    [Fact]
    public async Task HandleAccountUpdated_UnknownAccount_NoOpWithoutException()
    {
        var repo = new FakeCreatorRepository { CreatorByStripeConnectAccountId = null };
        var uow = new FakeCreatorsUnitOfWork();
        var service = BuildService(repo, uow);

        var data = new AccountUpdatedData
        {
            AccountId = "acct_unknown",
            DetailsSubmitted = true,
            ChargesEnabled = true,
            PayoutsEnabled = true
        };

        var exception = await Record.ExceptionAsync(() => service.HandleAccountUpdatedAsync(data, CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(0, uow.SaveChangesCallCount);
    }

    private const string StripeSubscriptionId = "sub_test123";

    private static CreatorPlan BuildPlan(string code, string stripePriceId, int platformFeeBasisPoints) =>
        CreatorPlan.Create(code, code, null, 1000, Currency.Eur, BillingInterval.Monthly, platformFeeBasisPoints, stripePriceId, Now);

    private static SubscriptionChangedData BuildSubscriptionChangedData(
        string status = "active",
        bool cancelAtPeriodEnd = false,
        string? stripePriceId = null,
        string eventId = "evt_test123") => new()
    {
        EventId = eventId,
        StripeSubscriptionId = StripeSubscriptionId,
        StripeCustomerId = "cus_test123",
        Status = status,
        CancelAtPeriodEnd = cancelAtPeriodEnd,
        StripePriceId = stripePriceId,
        CurrentPeriodStart = Now,
        CurrentPeriodEnd = Now.AddMonths(1),
    };

    [Fact]
    public async Task HandleSubscriptionUpdated_KnownNewPrice_UpdatesPlan()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        var basicPlan = BuildPlan("basic", "price_basic", 500);
        var proPlan = BuildPlan("pro", "price_pro", 250);
        var subscription = CreatorSubscription.CreateFree(creator, basicPlan, Now);
        subscription.ActivateWithProvider(StripeSubscriptionId, Now, Now.AddMonths(1), Now);

        var subscriptionRepo = new FakeCreatorSubscriptionRepository { SubscriptionByProviderSubscriptionId = subscription };
        var planRepo = new FakeCreatorPlanRepository();
        planRepo.PlansByCode["pro"] = proPlan;
        var webhookFailureRepo = new FakeWebhookFailureRepository();
        var uow = new FakeCreatorsUnitOfWork();
        var service = BuildService(new FakeCreatorRepository(), subscriptionRepo, planRepo, webhookFailureRepo, uow);

        var data = BuildSubscriptionChangedData(stripePriceId: "price_pro");

        await service.HandleSubscriptionUpdatedAsync(data, CancellationToken.None);

        Assert.Equal("pro", subscription.Plan.Code);
        Assert.Empty(webhookFailureRepo.Added);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleSubscriptionUpdated_UnknownPrice_PlanUnchangedAndWebhookFailureRecorded()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        var basicPlan = BuildPlan("basic", "price_basic", 500);
        var subscription = CreatorSubscription.CreateFree(creator, basicPlan, Now);
        subscription.ActivateWithProvider(StripeSubscriptionId, Now, Now.AddMonths(1), Now);

        var subscriptionRepo = new FakeCreatorSubscriptionRepository { SubscriptionByProviderSubscriptionId = subscription };
        var planRepo = new FakeCreatorPlanRepository(); // no plan registered for "price_unknown"
        var webhookFailureRepo = new FakeWebhookFailureRepository();
        var uow = new FakeCreatorsUnitOfWork();
        var service = BuildService(new FakeCreatorRepository(), subscriptionRepo, planRepo, webhookFailureRepo, uow);

        var data = BuildSubscriptionChangedData(stripePriceId: "price_unknown", eventId: "evt_unknown_price");

        await service.HandleSubscriptionUpdatedAsync(data, CancellationToken.None);

        // The subscription must not fall onto a wrong/random plan — it simply stays on "basic".
        Assert.Equal("basic", subscription.Plan.Code);
        Assert.Single(webhookFailureRepo.Added);
        var failure = webhookFailureRepo.Added[0];
        Assert.Equal("stripe", failure.Provider);
        Assert.Equal("evt_unknown_price", failure.EventId);
        Assert.Equal("customer.subscription.updated", failure.EventType);
        Assert.False(failure.IsResolved);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task HandleSubscriptionUpdated_CancelAtPeriodEndTrue_SchedulesCancel()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        var basicPlan = BuildPlan("basic", "price_basic", 500);
        var subscription = CreatorSubscription.CreateFree(creator, basicPlan, Now);
        subscription.ActivateWithProvider(StripeSubscriptionId, Now, Now.AddMonths(1), Now);

        var subscriptionRepo = new FakeCreatorSubscriptionRepository { SubscriptionByProviderSubscriptionId = subscription };
        var uow = new FakeCreatorsUnitOfWork();
        var service = BuildService(new FakeCreatorRepository(), subscriptionRepo, new FakeCreatorPlanRepository(), new FakeWebhookFailureRepository(), uow);

        var data = BuildSubscriptionChangedData(cancelAtPeriodEnd: true);

        await service.HandleSubscriptionUpdatedAsync(data, CancellationToken.None);

        Assert.True(subscription.CancelAtPeriodEnd);
    }

    [Fact]
    public async Task HandleSubscriptionUpdated_CancelAtPeriodEndFalseAfterTrue_UndoesScheduledCancel()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        var basicPlan = BuildPlan("basic", "price_basic", 500);
        var subscription = CreatorSubscription.CreateFree(creator, basicPlan, Now);
        subscription.ActivateWithProvider(StripeSubscriptionId, Now, Now.AddMonths(1), Now);
        subscription.ScheduleCancel(Now);

        var subscriptionRepo = new FakeCreatorSubscriptionRepository { SubscriptionByProviderSubscriptionId = subscription };
        var uow = new FakeCreatorsUnitOfWork();
        var service = BuildService(new FakeCreatorRepository(), subscriptionRepo, new FakeCreatorPlanRepository(), new FakeWebhookFailureRepository(), uow);

        var data = BuildSubscriptionChangedData(cancelAtPeriodEnd: false);

        await service.HandleSubscriptionUpdatedAsync(data, CancellationToken.None);

        Assert.False(subscription.CancelAtPeriodEnd);
    }
}
