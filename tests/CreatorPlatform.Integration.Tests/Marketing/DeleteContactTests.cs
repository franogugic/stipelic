using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Exceptions;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Marketing;

[Collection(PostgresCollection.Name)]
public sealed class DeleteContactTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public DeleteContactTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    /// <summary>The production service over the real repositories, on its own context (a request scope).</summary>
    private async Task<IReadOnlyList<int>> DeleteAsync(TestData.SeededCreator creator, string email)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new ContactsService(
            new MarketingCreatorContextProvider(db),
            new ContactsRepository(db),
            new MarketingUnitOfWork(db),
            new CreatorUsageService(db));

        var result = await service.DeleteAsync(creator.Slug, creator.OwnerUserId, email, CancellationToken.None);
        return result.AffectedLandingPageIds;
    }

    [Fact]
    public async Task Delete_RemovesThisCreatorsCapturesAndSummary()
    {
        var creator = await _data.CreateCreatorAsync();
        var pageA = await _data.CreateLandingPageAsync(creator.CreatorId);
        var pageB = await _data.CreateLandingPageAsync(creator.CreatorId);
        var email = TestData.UniqueEmail();
        var otherContact = TestData.UniqueEmail();
        await _data.CaptureAsync(creator.CreatorId, pageA, email);
        await _data.CaptureAsync(creator.CreatorId, pageB, email);
        await _data.CaptureAsync(creator.CreatorId, pageA, otherContact);

        var affectedPages = await DeleteAsync(creator, email);

        Assert.Equal(0, await _data.CountCapturesAsync(creator.CreatorId, email));
        Assert.False(await _data.SummaryExistsAsync(creator.CreatorId, email));
        Assert.Equal([pageA, pageB], affectedPages.OrderBy(id => id));
        // The creator's other contacts are untouched.
        Assert.Equal(1, await _data.CountCapturesAsync(creator.CreatorId, otherContact));
        Assert.True(await _data.SummaryExistsAsync(creator.CreatorId, otherContact));
    }

    [Fact]
    public async Task Delete_NormalisesTheEmail()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var email = TestData.UniqueEmail();
        await _data.CaptureAsync(creator.CreatorId, page, email);

        await DeleteAsync(creator, $"  {email.ToUpperInvariant()} ");

        Assert.False(await _data.SummaryExistsAsync(creator.CreatorId, email));
        Assert.Equal(0, await _data.CountCapturesAsync(creator.CreatorId, email));
    }

    [Fact]
    public async Task Delete_LeavesAnotherCreatorsCapturesOfTheSameEmailUntouched()
    {
        var creator = await _data.CreateCreatorAsync();
        var otherCreator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var otherPage = await _data.CreateLandingPageAsync(otherCreator.CreatorId);
        var email = TestData.UniqueEmail();
        await _data.CaptureAsync(creator.CreatorId, page, email);
        await _data.CaptureAsync(otherCreator.CreatorId, otherPage, email);
        var otherUsageBefore = await _data.GetContactsUsageAsync(otherCreator.CreatorId);

        await DeleteAsync(creator, email);

        Assert.Equal(1, await _data.CountCapturesAsync(otherCreator.CreatorId, email));
        Assert.True(await _data.SummaryExistsAsync(otherCreator.CreatorId, email));
        Assert.Equal(otherUsageBefore, await _data.GetContactsUsageAsync(otherCreator.CreatorId));
    }

    [Fact]
    public async Task Delete_KeepsUnsubscribesAndCampaignRecipients()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var email = TestData.UniqueEmail();
        await _data.CaptureAsync(creator.CreatorId, page, email);
        await _data.UnsubscribeAsync(creator.CreatorId, email);
        await _data.AddCampaignRecipientAsync(creator.CreatorId, email);

        await DeleteAsync(creator, email);

        Assert.False(await _data.SummaryExistsAsync(creator.CreatorId, email));
        Assert.True(await _data.UnsubscribeExistsAsync(creator.CreatorId, email));
        Assert.Equal(1, await _data.CountCampaignRecipientsAsync(creator.CreatorId, email));
    }

    [Fact]
    public async Task Delete_UnknownEmail_ThrowsNotFoundAndChangesNothing()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var existing = TestData.UniqueEmail();
        await _data.CaptureAsync(creator.CreatorId, page, existing);
        var usageBefore = await _data.GetContactsUsageAsync(creator.CreatorId);

        await Assert.ThrowsAsync<NotFoundException>(() => DeleteAsync(creator, TestData.UniqueEmail("unknown")));

        Assert.True(await _data.SummaryExistsAsync(creator.CreatorId, existing));
        Assert.Equal(1, await _data.CountCapturesAsync(creator.CreatorId, existing));
        Assert.Equal(usageBefore, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task Delete_EmailKnownOnlyToAnotherCreator_ThrowsNotFoundAndDeletesNothing()
    {
        var creator = await _data.CreateCreatorAsync();
        var otherCreator = await _data.CreateCreatorAsync();
        var otherPage = await _data.CreateLandingPageAsync(otherCreator.CreatorId);
        var email = TestData.UniqueEmail();
        await _data.CaptureAsync(otherCreator.CreatorId, otherPage, email);

        await Assert.ThrowsAsync<NotFoundException>(() => DeleteAsync(creator, email));

        Assert.True(await _data.SummaryExistsAsync(otherCreator.CreatorId, email));
        Assert.Equal(1, await _data.CountCapturesAsync(otherCreator.CreatorId, email));
    }

    [Fact]
    public async Task Delete_RefundsQuotaByTheNumberOfDeletedCaptures()
    {
        var creator = await _data.CreateCreatorAsync();
        var pageA = await _data.CreateLandingPageAsync(creator.CreatorId);
        var pageB = await _data.CreateLandingPageAsync(creator.CreatorId);
        var email = TestData.UniqueEmail();
        var keptContact = TestData.UniqueEmail();
        await _data.CaptureAsync(creator.CreatorId, pageA, email);
        await _data.CaptureAsync(creator.CreatorId, pageB, email);
        await _data.CaptureAsync(creator.CreatorId, pageA, keptContact);
        Assert.Equal(3, await _data.GetContactsUsageAsync(creator.CreatorId));

        await DeleteAsync(creator, email);

        Assert.Equal(1, await _data.GetContactsUsageAsync(creator.CreatorId));
    }
}
