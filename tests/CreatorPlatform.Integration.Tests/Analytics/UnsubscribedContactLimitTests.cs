using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>
/// F5.0: max_contacts counts contacts that are not unsubscribed. The counter always equals the creator's
/// contact_summaries rows without an unsubscribe: an unsubscribe gives a place back once, a delete only for an active
/// contact, and an unsubscribed email that signs up again stays unsubscribed and uncounted.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UnsubscribedContactLimitTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public UnsubscribedContactLimitTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task UnsubscribeAsync(int creatorId, string email, UnsubscribeSource source = UnsubscribeSource.Link)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new UnsubscribeService(
            new UnsubscribeRepository(db), new ContactsRepository(db), new CreatorUsageService(db), new MarketingUnitOfWork(db));
        await service.UnsubscribeAsync(creatorId, email, source, None);
    }

    private async Task DeleteContactAsync(TestData.SeededCreator creator, string email)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new ContactsService(
            new MarketingCreatorContextProvider(db), new ContactsRepository(db), new MarketingUnitOfWork(db),
            new CreatorUsageService(db), new UnsubscribeRepository(db));
        await service.DeleteAsync(creator.Slug, creator.OwnerUserId, email, None);
    }

    /// <summary>The F5.0 invariant: contact rows of the creator whose email has no unsubscribe.</summary>
    private async Task<int> CountedContactsAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value"
            FROM marketing.contact_summaries cs
            WHERE cs."CreatorId" = {creatorId}
              AND NOT EXISTS (SELECT 1 FROM marketing.unsubscribes u WHERE u."CreatorId" = cs."CreatorId" AND u."Email" = cs."Email")
            """).SingleAsync();
    }

    private async Task AssertInvariantAsync(int creatorId, int expected)
    {
        Assert.Equal(expected, await _data.GetContactsUsageAsync(creatorId));
        Assert.Equal(expected, await CountedContactsAsync(creatorId));
    }

    private async Task<(TestData.SeededCreator Creator, int Page, List<string> Emails)> CreatorWithContactsAsync(int count)
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var emails = Enumerable.Range(0, count).Select(_ => TestData.UniqueEmail()).ToList();
        foreach (var email in emails)
            await _data.CaptureAsync(creator.CreatorId, page, email);
        return (creator, page, emails);
    }

    [Fact]
    public async Task AnUnsubscribe_FreesAPlace_AndARepeatFreesNothing()
    {
        var (creator, _, emails) = await CreatorWithContactsAsync(2);

        await UnsubscribeAsync(creator.CreatorId, emails[0]);
        await AssertInvariantAsync(creator.CreatorId, 1);

        await UnsubscribeAsync(creator.CreatorId, emails[0]);
        await UnsubscribeAsync(creator.CreatorId, $"  {emails[0].ToUpperInvariant()} ", UnsubscribeSource.OneClick);

        await AssertInvariantAsync(creator.CreatorId, 1);
        Assert.True(await _data.UnsubscribeExistsAsync(creator.CreatorId, emails[0]));
        Assert.True(await _data.SummaryExistsAsync(creator.CreatorId, emails[0]));
    }

    [Fact]
    public async Task ParallelUnsubscribes_FreeThePlaceOnce()
    {
        var (creator, _, emails) = await CreatorWithContactsAsync(3);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => UnsubscribeAsync(creator.CreatorId, emails[0])));

        await AssertInvariantAsync(creator.CreatorId, 2);
    }

    [Fact]
    public async Task UnsubscribingAnEmailThatIsNotAContact_ChangesNothing()
    {
        var (creator, _, _) = await CreatorWithContactsAsync(1);

        await UnsubscribeAsync(creator.CreatorId, TestData.UniqueEmail());

        await AssertInvariantAsync(creator.CreatorId, 1);
    }

    [Fact]
    public async Task DeletingAnUnsubscribedContact_GivesNothingBack_DeletingAnActiveOneGivesOne()
    {
        var (creator, _, emails) = await CreatorWithContactsAsync(3);
        await UnsubscribeAsync(creator.CreatorId, emails[0]);
        await AssertInvariantAsync(creator.CreatorId, 2);

        await DeleteContactAsync(creator, emails[0]);
        await AssertInvariantAsync(creator.CreatorId, 2);
        Assert.True(await _data.UnsubscribeExistsAsync(creator.CreatorId, emails[0]));

        await DeleteContactAsync(creator, emails[1]);
        await AssertInvariantAsync(creator.CreatorId, 1);
    }

    [Fact]
    public async Task AnUnsubscribeRacingADeleteOfTheSameContact_GivesThePlaceBackOnce()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var (creator, _, emails) = await CreatorWithContactsAsync(2);

            await Task.WhenAll(UnsubscribeAsync(creator.CreatorId, emails[0]), DeleteContactAsync(creator, emails[0]));

            await AssertInvariantAsync(creator.CreatorId, 1);
        }
    }

    [Fact]
    public async Task AtTheLimit_AnUnsubscribe_MakesRoomForExactlyOneNewSignUp()
    {
        var (creator, page, emails) = await CreatorWithContactsAsync(1);
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit);
        await Assert.ThrowsAsync<ConflictException>(() => _data.CaptureAsync(creator.CreatorId, page, TestData.UniqueEmail()));

        await UnsubscribeAsync(creator.CreatorId, emails[0]);
        await _data.CaptureAsync(creator.CreatorId, page, TestData.UniqueEmail());

        Assert.Equal(TestData.FreePlanContactLimit, await _data.GetContactsUsageAsync(creator.CreatorId));
        await Assert.ThrowsAsync<ConflictException>(() => _data.CaptureAsync(creator.CreatorId, page, TestData.UniqueEmail()));
    }

    [Fact]
    public async Task AnUnsubscribedContactSigningUpAgain_StaysUnsubscribed_UncountedAndUnblocked()
    {
        var (creator, _, emails) = await CreatorWithContactsAsync(2);
        var otherPage = await _data.CreateLandingPageAsync(creator.CreatorId);
        await UnsubscribeAsync(creator.CreatorId, emails[0]);
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit);

        await _data.CaptureAsync(creator.CreatorId, otherPage, emails[0]);

        Assert.True(await _data.UnsubscribeExistsAsync(creator.CreatorId, emails[0]));
        Assert.Equal(2, await _data.CountCapturesAsync(creator.CreatorId, emails[0]));
        Assert.Equal(TestData.FreePlanContactLimit, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task AnUnsubscribedEmailWhoseContactWasDeleted_SigningUpAgain_IsAContactAgain_ButNotCounted()
    {
        var (creator, page, emails) = await CreatorWithContactsAsync(2);
        await UnsubscribeAsync(creator.CreatorId, emails[0]);
        await DeleteContactAsync(creator, emails[0]);
        await AssertInvariantAsync(creator.CreatorId, 1);

        await _data.CaptureAsync(creator.CreatorId, page, emails[0]);

        Assert.True(await _data.SummaryExistsAsync(creator.CreatorId, emails[0]));
        Assert.True(await _data.UnsubscribeExistsAsync(creator.CreatorId, emails[0]));
        await AssertInvariantAsync(creator.CreatorId, 1);

        // Not blocked at the limit either: it takes no place.
        await DeleteContactAsync(creator, emails[0]);
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit);
        await _data.CaptureAsync(creator.CreatorId, page, emails[0]);
        Assert.Equal(TestData.FreePlanContactLimit, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task TheRecount_LeavesUnsubscribedContactsOut_IsIdempotent_AndItsDownRestoresTheOldCount()
    {
        var (mixed, _, mixedEmails) = await CreatorWithContactsAsync(3);
        await InsertUnsubscribeAsync(mixed.CreatorId, mixedEmails[0]);
        await _data.SetContactsUsageAsync(mixed.CreatorId, 5);

        var (allOut, _, allOutEmails) = await CreatorWithContactsAsync(1);
        await InsertUnsubscribeAsync(allOut.CreatorId, allOutEmails[0]);

        for (var run = 0; run < 2; run++)
        {
            await using var db = _fixture.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync(RecountContactLimitWithoutUnsubscribed.RecalculateContactsUsageSql);

            await AssertInvariantAsync(mixed.CreatorId, 2);
            Assert.Equal(1, await _data.CountContactsUsageRowsAsync(mixed.CreatorId));
            await AssertInvariantAsync(allOut.CreatorId, 0);
            Assert.Equal(0, await _data.CountContactsUsageRowsAsync(allOut.CreatorId));
        }

        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync(RecountContactsTowardContactLimit.RecalculateContactsUsageSql);
        Assert.Equal(3, await _data.GetContactsUsageAsync(mixed.CreatorId));

        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync(RecountContactLimitWithoutUnsubscribed.RecalculateContactsUsageSql);
    }

    /// <summary>An unsubscribe written before F5.0, when it did not touch the counter.</summary>
    private async Task InsertUnsubscribeAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        await new UnsubscribeRepository(db).AddIfNotExistsAsync(
            Unsubscribe.Create(creatorId, email, UnsubscribeSource.Link, DateTimeOffset.UtcNow), None);
    }
}
