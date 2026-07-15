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
        return new CreatorWebhookService(
            creatorRepository,
            new FakeCreatorSubscriptionRepository(),
            new FakeCreatorPlanRepository(),
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
}
