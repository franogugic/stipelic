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
        var context = new CreatorContext(1, 5, 1) { PayoutMode = PayoutMode.StripeConnect, StripeConnectPayoutsEnabled = false };
        var (service, repository) = BuildService(context, LandingPageType.Sales);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));

        Assert.NotEqual(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }

    [Fact]
    public async Task PublishAsync_SalesNotPayoutReady_BankTransfer_Throws()
    {
        var context = new CreatorContext(1, 5, 1) { PayoutMode = PayoutMode.BankTransfer, HasPayoutProfile = false };
        var (service, repository) = BuildService(context, LandingPageType.Sales);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAsync_SalesPayoutReady_Publishes()
    {
        var context = new CreatorContext(1, 5, 1) { PayoutMode = PayoutMode.StripeConnect, StripeConnectPayoutsEnabled = true };
        var (service, repository) = BuildService(context, LandingPageType.Sales);

        await service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None);

        Assert.Equal(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }

    [Fact]
    public async Task PublishAsync_LeadGenNotPayoutReady_Publishes()
    {
        var context = new CreatorContext(1, 5, 1) { PayoutMode = PayoutMode.StripeConnect, StripeConnectPayoutsEnabled = false };
        var (service, repository) = BuildService(context, LandingPageType.LeadGen);

        await service.PublishAsync(CreatorSlug, repository.PageForUpdate!.PublicId, OwnerUserId, CancellationToken.None);

        Assert.Equal(LandingPageStatus.Published, repository.PageForUpdate!.Status);
    }
}
