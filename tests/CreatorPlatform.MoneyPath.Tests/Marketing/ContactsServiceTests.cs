using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class ContactsServiceTests
{
    private const string Slug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;
    private static readonly Guid CreatorPublicId = Guid.NewGuid();

    private static (ContactsService Service, FakeMarketingCreatorContextProvider ContextProvider, FakeContactsRepository Repository) BuildService()
    {
        var (service, contextProvider, repository, _, _) = BuildServiceWithFakes();
        return (service, contextProvider, repository);
    }

    private static (
        ContactsService Service,
        FakeMarketingCreatorContextProvider ContextProvider,
        FakeContactsRepository Repository,
        FakeMarketingUnitOfWork UnitOfWork,
        FakeCreatorUsageService UsageService) BuildServiceWithFakes()
    {
        var contextProvider = new FakeMarketingCreatorContextProvider
        {
            Context = new MarketingCreatorContext(CreatorId, CreatorPublicId, "Acme", Slug, "support@acme.test", "Acme", null, "#111111", "owner@acme.test"),
        };
        var repository = new FakeContactsRepository();
        var unitOfWork = new FakeMarketingUnitOfWork();
        var usageService = new FakeCreatorUsageService();
        var service = new ContactsService(contextProvider, repository, unitOfWork, usageService);

        return (service, contextProvider, repository, unitOfWork, usageService);
    }

    private static ContactRow BuildRow(string email) =>
        new(email, DateTimeOffset.UtcNow, 1, "Landing Page", false, [new ContactSourceRow(Guid.NewGuid(), "Landing Page")]);

    [Fact]
    public async Task SearchAsync_UnknownCreator_ThrowsNotFound()
    {
        var (service, contextProvider, _) = BuildService();
        contextProvider.Context = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SearchAsync(Slug, OwnerUserId, null, null, null, 50, CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_ExactlyLimitRowsReturned_HasMoreIsFalse()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [BuildRow("a@test.com"), BuildRow("b@test.com")];

        var page = await service.SearchAsync(Slug, OwnerUserId, null, null, null, 2, CancellationToken.None);

        Assert.Equal(2, page.Contacts.Count);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task SearchAsync_MoreRowsThanLimit_HasMoreIsTrueAndExtraRowTrimmed()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [BuildRow("a@test.com"), BuildRow("b@test.com"), BuildRow("c@test.com")];

        var page = await service.SearchAsync(Slug, OwnerUserId, null, null, null, 2, CancellationToken.None);

        Assert.Equal(2, page.Contacts.Count);
        Assert.True(page.HasMore);
        Assert.DoesNotContain(page.Contacts, c => c.Email == "c@test.com");
    }

    [Fact]
    public async Task SearchAsync_ZeroOrNegativeLimit_FallsBackToDefault()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [BuildRow("a@test.com")];

        await service.SearchAsync(Slug, OwnerUserId, null, null, null, 0, CancellationToken.None);

        Assert.Equal(50, repository.LastCall!.Value.Limit);
    }

    [Fact]
    public async Task SearchAsync_LimitAboveMax_IsClamped()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [BuildRow("a@test.com")];

        await service.SearchAsync(Slug, OwnerUserId, null, null, null, 5000, CancellationToken.None);

        Assert.Equal(100, repository.LastCall!.Value.Limit);
    }

    [Fact]
    public async Task SearchAsync_PassesSearchAndAfterEmailThrough()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [];

        await service.SearchAsync(Slug, OwnerUserId, "lead", null, "a@test.com", 50, CancellationToken.None);

        Assert.Equal(CreatorId, repository.LastCall!.Value.CreatorId);
        Assert.Equal("lead", repository.LastCall!.Value.Search);
        Assert.Equal("a@test.com", repository.LastCall!.Value.AfterEmail);
    }

    [Fact]
    public async Task DeleteAsync_UnknownContact_ThrowsNotFoundAndRefundsNothing()
    {
        var (service, _, repository, _, usageService) = BuildServiceWithFakes();
        repository.DeletionResult = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.DeleteAsync(Slug, OwnerUserId, "nobody@test.com", CancellationToken.None));

        Assert.Empty(usageService.RefundCalls);
    }

    [Fact]
    public async Task DeleteAsync_UnknownCreator_ThrowsNotFoundWithoutDeleting()
    {
        var (service, contextProvider, repository, _, _) = BuildServiceWithFakes();
        contextProvider.Context = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.DeleteAsync(Slug, OwnerUserId, "a@test.com", CancellationToken.None));

        Assert.Null(repository.LastDeleteCall);
    }

    [Fact]
    public async Task DeleteAsync_BlankEmail_ThrowsNotFoundWithoutDeleting()
    {
        var (service, _, repository, _, _) = BuildServiceWithFakes();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.DeleteAsync(Slug, OwnerUserId, "   ", CancellationToken.None));

        Assert.Null(repository.LastDeleteCall);
    }

    [Fact]
    public async Task DeleteAsync_NormalisesEmailRefundsCapturesInTransactionAndReturnsAffectedPages()
    {
        var (service, _, repository, unitOfWork, usageService) = BuildServiceWithFakes();
        repository.DeletionResult = new ContactDeletionRow([11, 12, 13]);

        var result = await service.DeleteAsync(Slug, OwnerUserId, "  Lead@Test.COM ", CancellationToken.None);

        Assert.Equal((CreatorId, "lead@test.com"), repository.LastDeleteCall);
        Assert.True(unitOfWork.TransactionCommitted);
        var refund = Assert.Single(usageService.RefundCalls);
        Assert.Equal((CreatorId, "max_contacts", 3, UsagePeriod.AllTime), (refund.CreatorId, refund.UsageKey, refund.Amount, refund.Period));
        Assert.Equal([11, 12, 13], result.AffectedLandingPageIds.OrderBy(id => id));
    }

    [Fact]
    public async Task DeleteAsync_SummaryWithoutCaptures_DeletesWithoutRefund()
    {
        var (service, _, repository, _, usageService) = BuildServiceWithFakes();
        repository.DeletionResult = new ContactDeletionRow([]);

        var result = await service.DeleteAsync(Slug, OwnerUserId, "a@test.com", CancellationToken.None);

        Assert.Empty(usageService.RefundCalls);
        Assert.Empty(result.AffectedLandingPageIds);
    }
}
