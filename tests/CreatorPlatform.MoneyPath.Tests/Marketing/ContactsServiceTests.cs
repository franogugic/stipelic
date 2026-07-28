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
        var contextProvider = new FakeMarketingCreatorContextProvider
        {
            Context = new MarketingCreatorContext(CreatorId, CreatorPublicId, "Acme", Slug, "support@acme.test", "Acme", null, "#111111", "owner@acme.test"),
        };
        var repository = new FakeContactsRepository();
        var service = new ContactsService(contextProvider, repository);

        return (service, contextProvider, repository);
    }

    private static ContactRow BuildRow(string email) =>
        new(email, DateTimeOffset.UtcNow, 1, "Landing Page", false);

    [Fact]
    public async Task SearchAsync_UnknownCreator_ThrowsNotFound()
    {
        var (service, contextProvider, _) = BuildService();
        contextProvider.Context = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.SearchAsync(Slug, OwnerUserId, null, null, 50, CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_ExactlyLimitRowsReturned_HasMoreIsFalse()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [BuildRow("a@test.com"), BuildRow("b@test.com")];

        var page = await service.SearchAsync(Slug, OwnerUserId, null, null, 2, CancellationToken.None);

        Assert.Equal(2, page.Contacts.Count);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task SearchAsync_MoreRowsThanLimit_HasMoreIsTrueAndExtraRowTrimmed()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [BuildRow("a@test.com"), BuildRow("b@test.com"), BuildRow("c@test.com")];

        var page = await service.SearchAsync(Slug, OwnerUserId, null, null, 2, CancellationToken.None);

        Assert.Equal(2, page.Contacts.Count);
        Assert.True(page.HasMore);
        Assert.DoesNotContain(page.Contacts, c => c.Email == "c@test.com");
    }

    [Fact]
    public async Task SearchAsync_ZeroOrNegativeLimit_FallsBackToDefault()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [BuildRow("a@test.com")];

        await service.SearchAsync(Slug, OwnerUserId, null, null, 0, CancellationToken.None);

        Assert.Equal(50, repository.LastCall!.Value.Limit);
    }

    [Fact]
    public async Task SearchAsync_LimitAboveMax_IsClamped()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [BuildRow("a@test.com")];

        await service.SearchAsync(Slug, OwnerUserId, null, null, 5000, CancellationToken.None);

        Assert.Equal(100, repository.LastCall!.Value.Limit);
    }

    [Fact]
    public async Task SearchAsync_PassesSearchAndAfterEmailThrough()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = [];

        await service.SearchAsync(Slug, OwnerUserId, "lead", "a@test.com", 50, CancellationToken.None);

        Assert.Equal(CreatorId, repository.LastCall!.Value.CreatorId);
        Assert.Equal("lead", repository.LastCall!.Value.Search);
        Assert.Equal("a@test.com", repository.LastCall!.Value.AfterEmail);
    }
}
