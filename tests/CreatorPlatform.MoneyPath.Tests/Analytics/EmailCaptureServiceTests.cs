using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Analytics;

public class EmailCaptureServiceTests
{
    private const int CreatorId = 1;
    private const int LandingPageId = 10;

    private static (EmailCaptureService Service, FakeEmailCaptureRepository Repository, FakeAnalyticsCreatorContextProvider ContextProvider, FakeCreatorUsageService UsageService, FakeAnalyticsUnitOfWork UnitOfWork)
        BuildService(int? planLimit = 500)
    {
        var repository = new FakeEmailCaptureRepository();
        var contextProvider = new FakeAnalyticsCreatorContextProvider { PlanLimit = planLimit };
        var usageService = new FakeCreatorUsageService();
        var unitOfWork = new FakeAnalyticsUnitOfWork();

        var service = new EmailCaptureService(repository, contextProvider, usageService, unitOfWork, new FakeLeadMagnetDelivery());

        return (service, repository, contextProvider, usageService, unitOfWork);
    }

    [Fact]
    public async Task CaptureAsync_NoActiveSubscription_ThrowsConflictAndWritesNothing()
    {
        var (service, repository, _, _, _) = BuildService(planLimit: null);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CaptureAsync(LandingPageId, null, CreatorId, "visitor@example.com", CancellationToken.None));

        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CaptureAsync_NewContactAtLimit_ThrowsConflictAndRollsBack()
    {
        var (service, _, _, usageService, unitOfWork) = BuildService(planLimit: 500);
        usageService.Used[(CreatorId, "max_contacts")] = 500;

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CaptureAsync(LandingPageId, null, CreatorId, "visitor@example.com", CancellationToken.None));

        Assert.False(unitOfWork.LastTransactionCommitted);
        Assert.Equal(500, usageService.Used[(CreatorId, "max_contacts")]);
    }

    [Fact]
    public async Task CaptureAsync_NewContactUnderLimit_InsertsAndConsumesOneUnitOfUsage()
    {
        var (service, repository, _, usageService, unitOfWork) = BuildService(planLimit: 500);
        usageService.Used[(CreatorId, "max_contacts")] = 100;

        await service.CaptureAsync(LandingPageId, null, CreatorId, "Visitor@Example.com", CancellationToken.None);

        Assert.Single(repository.Added);
        Assert.Equal("visitor@example.com", repository.Added[0].Email);
        Assert.Equal(101, usageService.Used[(CreatorId, "max_contacts")]);
        Assert.True(unitOfWork.LastTransactionCommitted);

        var upsert = Assert.Single(repository.ContactSummaryUpserts);
        Assert.Equal(CreatorId, upsert.CreatorId);
        Assert.Equal(LandingPageId, upsert.LandingPageId);
        Assert.Equal("visitor@example.com", upsert.Email);
    }

    [Fact]
    public async Task CaptureAsync_KnownContactOnAnotherPage_UsesNoSlotEvenAtTheLimit()
    {
        var (service, repository, _, usageService, unitOfWork) = BuildService(planLimit: 500);
        usageService.Used[(CreatorId, "max_contacts")] = 499;
        await service.CaptureAsync(LandingPageId, null, CreatorId, "visitor@example.com", CancellationToken.None);

        await service.CaptureAsync(LandingPageId + 1, null, CreatorId, "visitor@example.com", CancellationToken.None);

        Assert.Equal(2, repository.Added.Count);
        Assert.Equal(500, usageService.Used[(CreatorId, "max_contacts")]);
        Assert.True(unitOfWork.LastTransactionCommitted);
    }

    [Fact]
    public async Task CaptureAsync_DuplicateCapture_DoesNotIncrementUsage()
    {
        var (service, repository, _, usageService, _) = BuildService(planLimit: 500);
        usageService.Used[(CreatorId, "max_contacts")] = 100;
        repository.NextInsertResult = false; // simulate ON CONFLICT DO NOTHING (already captured)

        await service.CaptureAsync(LandingPageId, null, CreatorId, "visitor@example.com", CancellationToken.None);

        Assert.Empty(repository.Added);
        Assert.Equal(100, usageService.Used[(CreatorId, "max_contacts")]);
        Assert.Empty(repository.ContactSummaryUpserts);
    }

    [Fact]
    public async Task CaptureAsync_UnlimitedPlan_AlwaysAllowedRegardlessOfUsage()
    {
        var (service, repository, _, usageService, _) = BuildService(planLimit: -1);
        usageService.Used[(CreatorId, "max_contacts")] = 1_000_000;

        await service.CaptureAsync(LandingPageId, null, CreatorId, "visitor@example.com", CancellationToken.None);

        Assert.Single(repository.Added);
    }

    // ---- F8: the lead page's document ---------------------------------------------------------------------------

    private static (EmailCaptureService Service, FakeCreatorUsageService UsageService, FakeLeadMagnetDelivery Delivery, FakeEmailCaptureRepository Repository)
        BuildWithDelivery()
    {
        var repository = new FakeEmailCaptureRepository();
        var usageService = new FakeCreatorUsageService();
        var delivery = new FakeLeadMagnetDelivery();
        var service = new EmailCaptureService(
            repository, new FakeAnalyticsCreatorContextProvider { PlanLimit = 500 }, usageService, new FakeAnalyticsUnitOfWork(), delivery);
        return (service, usageService, delivery, repository);
    }

    [Fact]
    public async Task ANewAndARepeatSignUp_BothAskForTheDocument_WithTheNormalisedEmail()
    {
        var (service, _, delivery, _) = BuildWithDelivery();

        await service.CaptureAsync(LandingPageId, 3, CreatorId, " Visitor@Example.com ", CancellationToken.None);
        await service.CaptureAsync(LandingPageId, 3, CreatorId, "visitor@example.com", CancellationToken.None);

        // The delivery decides about the 24-hour re-send; the capture always asks.
        Assert.Equal([(CreatorId, LandingPageId, "visitor@example.com"), (CreatorId, LandingPageId, "visitor@example.com")], delivery.Deliveries);
    }

    [Fact]
    public async Task ASignUpRejectedByTheContactLimit_SendsNothing()
    {
        var (service, usageService, delivery, _) = BuildWithDelivery();
        usageService.Used[(CreatorId, "max_contacts")] = 500;

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CaptureAsync(LandingPageId, 3, CreatorId, "visitor@example.com", CancellationToken.None));

        Assert.Empty(delivery.Deliveries);
    }

    [Fact]
    public async Task AnUnsubscribedEmail_StillGetsTheDocumentItAskedFor()
    {
        var (service, usageService, delivery, repository) = BuildWithDelivery();
        repository.UnsubscribedEmails.Add("visitor@example.com");
        usageService.Used[(CreatorId, "max_contacts")] = 500;

        await service.CaptureAsync(LandingPageId, 3, CreatorId, "visitor@example.com", CancellationToken.None);

        Assert.Single(delivery.Deliveries);
    }
}
