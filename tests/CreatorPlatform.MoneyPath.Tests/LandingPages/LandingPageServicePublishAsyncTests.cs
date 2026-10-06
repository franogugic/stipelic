using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Application.Services;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.LandingPages;

public class LandingPageServicePublishAsyncTests
{
    private const string CreatorSlug = "acme";
    private const int OwnerUserId = 1;

    private static (LandingPageService Service, FakeLandingPageRepository Repository) BuildService(
        CreatorContext context, LandingPageType type)
    {
        var landingPage = LandingPage.Create(context.CreatorId, productId: 1, "Title", "slug", type, DateTimeOffset.UtcNow);
        var repository = new FakeLandingPageRepository { PageForUpdate = landingPage };
        var sectionRepository = new FakeLandingPageSectionRepository();
        var contextProvider = new FakeLandingPagesCreatorContextProvider { Context = context };
        var unitOfWork = new FakeLandingPagesUnitOfWork();

        var service = new LandingPageService(repository, sectionRepository, contextProvider, unitOfWork);

        return (service, repository);
    }

    [Fact]
    public async Task PublishAsync_SalesNotPayoutReady_StripeConnect_Throws()
    {
        var context = new CreatorContext(1, 5, 1) { Status = CreatorStatus.Active, PayoutMode = PayoutMode.StripeConnect, StripeConnectPayoutsEnabled = false };
        var (service, repository) = BuildService(context, LandingPageType.Sales);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));

        Assert.NotEqual(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }

    [Fact]
    public async Task PublishAsync_SalesNotPayoutReady_BankTransfer_Throws()
    {
        var context = new CreatorContext(1, 5, 1) { Status = CreatorStatus.Active, PayoutMode = PayoutMode.BankTransfer, HasPayoutProfile = false };
        var (service, repository) = BuildService(context, LandingPageType.Sales);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_SalesPayoutReady_Publishes()
    {
        var context = new CreatorContext(1, 5, 1) { Status = CreatorStatus.Active, PayoutMode = PayoutMode.StripeConnect, StripeConnectPayoutsEnabled = true };
        var (service, repository) = BuildService(context, LandingPageType.Sales);

        await service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None);

        Assert.Equal(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }

    [Fact]
    public async Task PublishAsync_LeadGenNotPayoutReady_Publishes()
    {
        var context = new CreatorContext(1, 5, 1) { Status = CreatorStatus.Active, PayoutMode = PayoutMode.StripeConnect, StripeConnectPayoutsEnabled = false };
        var (service, repository) = BuildService(context, LandingPageType.LeadGen);

        await service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None);

        Assert.Equal(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }

    [Fact]
    public async Task PublishAsync_PendingPayment_LeadGen_Throws()
    {
        var context = new CreatorContext(1, 5, 1) { Status = CreatorStatus.PendingPayment };
        var (service, repository) = BuildService(context, LandingPageType.LeadGen);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal("Complete your subscription payment before publishing.", exception.Message);
        Assert.NotEqual(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }

    [Fact]
    public async Task PublishAsync_PendingPayment_Sales_Throws()
    {
        var context = new CreatorContext(1, 5, 1)
        {
            Status = CreatorStatus.PendingPayment,
            PayoutMode = PayoutMode.StripeConnect,
            StripeConnectPayoutsEnabled = true,
        };
        var (service, repository) = BuildService(context, LandingPageType.Sales);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));

        Assert.NotEqual(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }

    // ---- Machine-readable block reasons ---------------------------------------------------------------------

    [Fact]
    public async Task PublishAsync_InactiveSubscription_HasTheSubscriptionInactiveCode()
    {
        var context = new CreatorContext(1, 5, 1) { Status = CreatorStatus.PendingPayment };
        var (service, repository) = BuildService(context, LandingPageType.LeadGen);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal(LandingPageService.SubscriptionInactiveCode, exception.Code);
    }

    [Fact]
    public async Task PublishAsync_SalesWithoutPayouts_HasThePayoutsNotReadyCode()
    {
        var context = new CreatorContext(1, 5, 1) { Status = CreatorStatus.Active, PayoutMode = PayoutMode.BankTransfer, HasPayoutProfile = false };
        var (service, repository) = BuildService(context, LandingPageType.Sales);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal(LandingPageService.PayoutsNotReadyCode, exception.Code);
    }

    [Fact]
    public async Task PublishAsync_OverThePageLimit_HasThePlanLimitCode_WithUsedAndLimit()
    {
        // e.g. after a downgrade to Free: 3 non-archived pages on a 1-page plan.
        var context = new CreatorContext(1, 1, 3) { Status = CreatorStatus.Active };
        var (service, repository) = BuildService(context, LandingPageType.LeadGen);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal(LandingPageService.PlanLimitReachedCode, exception.Code);
        Assert.Equal(new LandingPageService.PlanLimitDetails(Used: 3, Limit: 1), exception.Details);
        Assert.NotEqual(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }

    [Theory]
    [InlineData(1, 1)]   // at the limit
    [InlineData(-1, 50)] // unlimited plan
    public async Task PublishAsync_AtTheLimitOrUnlimited_Publishes(int maxLandingPages, int activeCount)
    {
        var context = new CreatorContext(1, maxLandingPages, activeCount) { Status = CreatorStatus.Active };
        var (service, repository) = BuildService(context, LandingPageType.LeadGen);

        await service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None);

        Assert.Equal(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }
}
