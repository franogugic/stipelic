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
        return new CreatorConnectService(creatorRepository, unitOfWork, connectAccountService, new FakePayoutScheduleCache(), options);
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

    // ---- Stripe Express dashboard login link ----------------------------------------------------------------

    private static Creator ConnectCreator(bool detailsSubmitted, string? accountId = "acct_dash")
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        if (accountId is not null)
            creator.SetStripeConnectAccountId(accountId, Now);
        creator.UpdateStripeConnectStatus(detailsSubmitted, detailsSubmitted, detailsSubmitted, Now, Now);
        return creator;
    }

    [Fact]
    public async Task DashboardLoginLink_ConnectedAccountWithDetailsSubmitted_ReturnsStripesLink()
    {
        var connect = new FakeConnectAccountService();
        var service = BuildService(new FakeCreatorRepository { CreatorByOwner = ConnectCreator(detailsSubmitted: true) }, new FakeCreatorsUnitOfWork(), connect);

        var result = await service.CreateDashboardLoginLinkAsync(ownerUserId: 1, CancellationToken.None);

        Assert.Equal("https://connect.stripe.com/express/login/acct_dash", result.Url);
        Assert.Equal(["acct_dash"], connect.LoginLinkRequestsFor);
    }

    [Fact]
    public async Task DashboardLoginLink_IsNeverCached_EachCallAsksStripeAgain()
    {
        var connect = new FakeConnectAccountService();
        var service = BuildService(new FakeCreatorRepository { CreatorByOwner = ConnectCreator(detailsSubmitted: true) }, new FakeCreatorsUnitOfWork(), connect);

        await service.CreateDashboardLoginLinkAsync(1, CancellationToken.None);
        await service.CreateDashboardLoginLinkAsync(1, CancellationToken.None);

        Assert.Equal(2, connect.LoginLinkRequestsFor.Count);
    }

    [Fact]
    public async Task DashboardLoginLink_NoWorkspace_Is404()
    {
        var service = BuildService(new FakeCreatorRepository { CreatorByOwner = null }, new FakeCreatorsUnitOfWork(), new FakeConnectAccountService());

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateDashboardLoginLinkAsync(1, CancellationToken.None));
    }

    public static TheoryData<string> UnavailableCases => ["bank-transfer", "no-account", "details-not-submitted"];

    [Theory]
    [MemberData(nameof(UnavailableCases))]
    public async Task DashboardLoginLink_WithoutACompletedConnectAccount_Is409(string scenario)
    {
        var creator = scenario switch
        {
            "bank-transfer" => Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "RS", PayoutMode.BankTransfer, Now),
            "no-account" => ConnectCreator(detailsSubmitted: false, accountId: null),
            _ => ConnectCreator(detailsSubmitted: false),
        };
        var connect = new FakeConnectAccountService();
        var service = BuildService(new FakeCreatorRepository { CreatorByOwner = creator }, new FakeCreatorsUnitOfWork(), connect);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => service.CreateDashboardLoginLinkAsync(1, CancellationToken.None));

        Assert.Equal("connect_dashboard_unavailable", exception.Code);
        Assert.Empty(connect.LoginLinkRequestsFor);
    }
}
