using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

public class ContactsExportTests
{
    private const string Slug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;

    private static (ContactsService Service, FakeMarketingCreatorContextProvider ContextProvider, FakeContactsRepository Repository) BuildService()
    {
        var contextProvider = new FakeMarketingCreatorContextProvider
        {
            Context = new MarketingCreatorContext(CreatorId, Guid.NewGuid(), "Acme", Slug, null, "Acme", null, "#111111", "owner@acme.test"),
        };
        var repository = new FakeContactsRepository();
        return (new ContactsService(contextProvider, repository, new FakeMarketingUnitOfWork(), new FakeCreatorUsageService()), contextProvider, repository);
    }

    [Fact]
    public void Row_JoinsSourcesInOrderAndFormatsDateAndStatus()
    {
        var contact = new ContactDto(
            "lead@example.com",
            new DateTimeOffset(2026, 7, 1, 8, 30, 0, TimeSpan.Zero),
            2,
            "ignored legacy label",
            true,
            [new ContactSourceDto(Guid.NewGuid(), "Spring Sale"), new ContactSourceDto(Guid.NewGuid(), "Guide, 2nd ed.")]);

        Assert.Equal(
            "lead@example.com,\"Spring Sale; Guide, 2nd ed.\",2026-07-01T08:30:00Z,unsubscribed",
            ContactsCsv.Row(contact));
    }

    [Fact]
    public void Row_ActiveContactWithAFormulaLikeTitle_IsGuarded()
    {
        var contact = new ContactDto(
            "a@example.com", DateTimeOffset.UnixEpoch, 1, "", false, [new ContactSourceDto(Guid.NewGuid(), "=cmd|' /C calc'!A0")]);

        Assert.Equal("a@example.com,'=cmd|' /C calc'!A0,1970-01-01T00:00:00Z,active", ContactsCsv.Row(contact));
    }

    [Fact]
    public void Header_AndFileName_MatchTheContract()
    {
        Assert.Equal("email,sources,joined_at,status", ContactsCsv.Header);
        Assert.Equal("subscribers-acme-2026-10-02.csv", ContactsCsv.FileName("acme", new DateTimeOffset(2026, 10, 2, 23, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task StartExportAsync_StreamsEveryContactAcrossKeysetBatchesInEmailOrder()
    {
        var (service, _, repository) = BuildService();
        repository.Rows = Enumerable.Range(0, 1203)
            .Select(i => new ContactRow($"c{i:D5}@example.com", DateTimeOffset.UnixEpoch, 1, "", false, []))
            .Reverse()
            .ToList();

        var export = await service.StartExportAsync(Slug, OwnerUserId, null, null, CancellationToken.None);
        var emails = new List<string>();
        await foreach (var contact in export.Contacts)
            emails.Add(contact.Email);

        Assert.Equal(1203, emails.Count);
        Assert.Equal(emails.OrderBy(e => e, StringComparer.Ordinal), emails);
        Assert.Equal(3, repository.SearchCallCount); // 500 + 500 + 203
        Assert.Equal(Slug, export.CreatorSlug);
    }

    [Fact]
    public async Task StartExportAsync_ForeignLandingPage_ThrowsNotFoundBeforeStreaming()
    {
        var (service, contextProvider, repository) = BuildService();
        contextProvider.LandingPageId = null;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.StartExportAsync(Slug, OwnerUserId, null, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(0, repository.SearchCallCount);
    }
}
