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

        var service = new EmailCaptureService(repository, contextProvider, usageService, unitOfWork);

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
    public async Task CaptureAsync_AtLimit_ThrowsConflictAndWritesNothing()
    {
        var (service, repository, _, usageService, _) = BuildService(planLimit: 500);
        usageService.Used[(CreatorId, "max_contacts")] = 500;

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CaptureAsync(LandingPageId, null, CreatorId, "visitor@example.com", CancellationToken.None));

        Assert.Empty(repository.Added);
    }

    [Fact]
    public async Task CaptureAsync_UnderLimit_InsertsAndConsumesOneUnitOfUsage()
    {
        var (service, repository, _, usageService, unitOfWork) = BuildService(planLimit: 500);
        usageService.Used[(CreatorId, "max_contacts")] = 100;

        await service.CaptureAsync(LandingPageId, null, CreatorId, "Visitor@Example.com", CancellationToken.None);

        Assert.Single(repository.Added);
        Assert.Equal("visitor@example.com", repository.Added[0].Email);
        Assert.Equal(101, usageService.Used[(CreatorId, "max_contacts")]);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);

        var upsert = Assert.Single(repository.ContactSummaryUpserts);
        Assert.Equal(CreatorId, upsert.CreatorId);
        Assert.Equal(LandingPageId, upsert.LandingPageId);
        Assert.Equal("visitor@example.com", upsert.Email);
    }

    [Fact]
    public async Task CaptureAsync_DuplicateCapture_DoesNotIncrementUsage()
    {
        var (service, repository, _, usageService, unitOfWork) = BuildService(planLimit: 500);
        usageService.Used[(CreatorId, "max_contacts")] = 100;
        repository.NextInsertResult = false; // simulate ON CONFLICT DO NOTHING (already captured)

        await service.CaptureAsync(LandingPageId, null, CreatorId, "visitor@example.com", CancellationToken.None);

        Assert.Empty(repository.Added);
        Assert.Equal(100, usageService.Used[(CreatorId, "max_contacts")]);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
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
}
