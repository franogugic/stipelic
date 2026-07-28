using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Creators;

public class CreatorConnectServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreatorConnectService BuildService(
        FakeCreatorRepository creatorRepository,
        FakeCreatorsUnitOfWork unitOfWork,
        FakeConnectAccountService connectAccountService)
    {
        var options = Options.Create(new StripeOptions { FrontendBaseUrl = "https://app.test" });
        return new CreatorConnectService(creatorRepository, unitOfWork, connectAccountService, options);
    }

    [Fact]
    public async Task StartConnectOnboarding_BankTransferCreator_Throws()
    {
        var callLog = new List<string>();
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "RS", PayoutMode.BankTransfer, Now);
        var repo = new FakeCreatorRepository(callLog) { CreatorByOwner = creator };
        var uow = new FakeCreatorsUnitOfWork(callLog);
        var connect = new FakeConnectAccountService(callLog);
        var service = BuildService(repo, uow, connect);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.StartConnectOnboardingAsync(1, "owner@test.com", CancellationToken.None));

        Assert.Equal(0, connect.CreateAccountCallCount);
    }

    [Fact]
    public async Task StartConnectOnboarding_ExistingAccountId_DoesNotCreateNewAccount()
    {
        var callLog = new List<string>();
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        creator.SetStripeConnectAccountId("acct_existing", Now);
        var repo = new FakeCreatorRepository(callLog) { CreatorByOwner = creator };
        var uow = new FakeCreatorsUnitOfWork(callLog);
        var connect = new FakeConnectAccountService(callLog);
        var service = BuildService(repo, uow, connect);

        var result = await service.StartConnectOnboardingAsync(1, "owner@test.com", CancellationToken.None);

        Assert.Equal(0, connect.CreateAccountCallCount);
        Assert.Equal(1, connect.CreateOnboardingLinkCallCount);
        Assert.Equal(connect.UrlToReturn, result.Url);
    }

    [Fact]
    public async Task StartConnectOnboarding_NewAccount_SavesAccountIdBeforeCreatingLink()
    {
        var callLog = new List<string>();
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        var repo = new FakeCreatorRepository(callLog) { CreatorByOwner = creator };
        var uow = new FakeCreatorsUnitOfWork(callLog);
        var connect = new FakeConnectAccountService(callLog);
        var service = BuildService(repo, uow, connect);

        var result = await service.StartConnectOnboardingAsync(1, "owner@test.com", CancellationToken.None);

        Assert.Equal(1, connect.CreateAccountCallCount);
        Assert.Equal(connect.AccountIdToReturn, creator.StripeConnectAccountId);
        // Order matters: the account id must be persisted (SaveChanges) before the onboarding link is
        // requested, so a failed link call never loses track of a just-created Stripe account.
        Assert.Equal(["CreateAccount", "SaveChanges", "CreateOnboardingLink"], callLog);
        Assert.Equal(connect.UrlToReturn, result.Url);
    }
}
