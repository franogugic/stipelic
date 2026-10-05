using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Marketing;

/// <summary>The Contacts directory search matches any part of the email, case-insensitively, with the term's own
/// wildcards taken literally — on the page list, with the source filter, across keyset pages and in the export.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ContactSearchTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public ContactSearchTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<T> WithServiceAsync<T>(Func<ContactsService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(new ContactsService(
            new MarketingCreatorContextProvider(db),
            new ContactsRepository(db),
            new MarketingUnitOfWork(db),
            new CreatorUsageService(db)));
    }

    private Task<List<string>> SearchAsync(
        TestData.SeededCreator creator, string? search, Guid? source = null, string? afterEmail = null, int limit = 50) =>
        WithServiceAsync(async service =>
            (await service.SearchAsync(creator.Slug, creator.OwnerUserId, search, source, afterEmail, limit, CancellationToken.None))
            .Contacts.Select(c => c.Email).ToList());

    [Fact]
    public async Task ASurnameInTheMiddleOfTheEmail_IsFound_CaseInsensitively()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        await SeedContactsAsync(creator.CreatorId, page, "ana.saric@example.test", "saric@example.test",
            "marko.horvat@example.test", "ivana@saric-studio.test");

        Assert.Equal(
            ["ana.saric@example.test", "ivana@saric-studio.test", "saric@example.test"],
            await SearchAsync(creator, "  SARIC "));
        Assert.Equal(["marko.horvat@example.test"], await SearchAsync(creator, "rvat@"));
        Assert.Equal(4, (await SearchAsync(creator, null)).Count);
    }

    [Fact]
    public async Task PercentUnderscoreAndBackslash_MatchLiterally()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        await SeedContactsAsync(creator.CreatorId, page,
            "100%real@example.test", "100xreal@example.test",
            "first_last@example.test", "firstxlast@example.test",
            @"back\slash@example.test", "backxslash@example.test");

        Assert.Equal(["100%real@example.test"], await SearchAsync(creator, "0%r"));
        Assert.Equal(["first_last@example.test"], await SearchAsync(creator, "t_l"));
        Assert.Equal([@"back\slash@example.test"], await SearchAsync(creator, @"k\s"));
        // A bare wildcard is a literal character too: only the email that contains it, not all six.
        Assert.Equal(["100%real@example.test"], await SearchAsync(creator, "%"));
        Assert.Equal(["first_last@example.test"], await SearchAsync(creator, "_"));
    }

    [Fact]
    public async Task CombinedWithTheSourceFilter()
    {
        var creator = await _data.CreateCreatorAsync();
        var pageA = await _data.CreateLandingPageAsync(creator.CreatorId);
        var pageB = await _data.CreateLandingPageAsync(creator.CreatorId);
        await SeedContactsAsync(creator.CreatorId, pageA, "ana.saric@example.test", "marko@example.test");
        await SeedContactsAsync(creator.CreatorId, pageB, "petra.saric@example.test");

        Assert.Equal(["ana.saric@example.test"], await SearchAsync(creator, "saric", await PublicIdAsync(pageA)));
        Assert.Equal(["petra.saric@example.test"], await SearchAsync(creator, "saric", await PublicIdAsync(pageB)));
    }

    [Fact]
    public async Task KeysetPagination_WalksTheMatchesInEmailOrder()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var matches = Enumerable.Range(1, 7).Select(n => $"user{n}.saric@example.test").ToArray();
        await SeedContactsAsync(creator.CreatorId, page, [.. matches, "other1@example.test", "other2@example.test"]);

        var walked = new List<string>();
        string? after = null;
        while (true)
        {
            var batch = await SearchAsync(creator, "saric", afterEmail: after, limit: 3);
            walked.AddRange(batch);
            if (batch.Count < 3)
                break;
            after = batch[^1];
        }

        Assert.Equal(matches.Order(StringComparer.Ordinal), walked);
    }

    [Fact]
    public async Task TheExport_UsesTheSameSearch()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        await SeedContactsAsync(creator.CreatorId, page, "ana.saric@example.test", "marko@example.test", "saric.b@example.test");

        var exported = await WithServiceAsync(async service =>
        {
            var export = await service.StartExportAsync(creator.Slug, creator.OwnerUserId, "saric", null, CancellationToken.None);
            var emails = new List<string>();
            await foreach (var contact in export.Contacts)
                emails.Add(contact.Email);
            return emails;
        });

        Assert.Equal(["ana.saric@example.test", "saric.b@example.test"], exported);
    }

    /// <summary>Summary rows exactly as the capture path stores them (lower-case email, one source page).</summary>
    private async Task SeedContactsAsync(int creatorId, int landingPageId, params string[] emails)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        foreach (var email in emails)
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO marketing.contact_summaries ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds")
                VALUES ({creatorId}, {email}, {now}, {now}, ARRAY[{landingPageId}])
                """);
        }
    }

    private async Task<Guid> PublicIdAsync(int landingPageId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<Guid>($"""
            SELECT "PublicId" AS "Value" FROM landing_pages.landing_pages WHERE "Id" = {landingPageId}
            """).ToListAsync()).Single();
    }
}
