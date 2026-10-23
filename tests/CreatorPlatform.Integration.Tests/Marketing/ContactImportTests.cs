using System.Text;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Contacts;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Marketing.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Marketing;

/// <summary>
/// CSV contact import (F5.1) on the real database: every row category in one file, the plan limit all or nothing with
/// the counter matching the inserted rows, the dry run writing nothing, unsubscribed emails never imported, consent and
/// ownership, and where imported contacts show up.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ContactImportTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public ContactImportTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<ContactImportSummaryDto> ImportAsync(
        TestData.SeededCreator creator, string csv, bool dryRun = false, bool consent = true, bool bom = false)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new ContactImportService(
            new MarketingCreatorContextProvider(db), new ContactCsvReader(), new ContactImportRepository(db),
            new CreatorUsageService(db), new MarketingUnitOfWork(db));
        var bytes = Encoding.UTF8.GetBytes(csv);
        if (bom)
            bytes = [0xEF, 0xBB, 0xBF, .. bytes];
        return await service.ImportAsync(creator.Slug, creator.OwnerUserId, "contacts.csv", new MemoryStream(bytes), consent, dryRun, None);
    }

    private async Task UnsubscribeAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        await new UnsubscribeService(new UnsubscribeRepository(db), new ContactsRepository(db), new CreatorUsageService(db), new MarketingUnitOfWork(db))
            .UnsubscribeAsync(creatorId, email, UnsubscribeSource.Link, None);
    }

    private async Task<List<ContactSummary>> ContactsAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Set<ContactSummary>().AsNoTracking().Where(c => c.CreatorId == creatorId).OrderBy(c => c.Email).ToListAsync();
    }

    private async Task<List<ContactImport>> ImportsAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Set<ContactImport>().AsNoTracking().Where(i => i.CreatorId == creatorId).ToListAsync();
    }

    /// <summary>The F5.0 invariant the counter must keep: contacts without an unsubscribe.</summary>
    private async Task<int> CountedContactsAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM marketing.contact_summaries cs
            WHERE cs."CreatorId" = {creatorId}
              AND NOT EXISTS (SELECT 1 FROM marketing.unsubscribes u WHERE u."CreatorId" = cs."CreatorId" AND u."Email" = cs."Email")
            """).SingleAsync();
    }

    private static string Unique(string name) => TestData.UniqueEmail(name);

    /// <summary>A creator with one page sign-up (<c>Existing</c>) and one unsubscribed contact (<c>Unsubscribed</c>).</summary>
    private async Task<(TestData.SeededCreator Creator, string Existing, string Unsubscribed)> CreatorAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var (existing, unsubscribed) = (Unique("existing"), Unique("unsub"));
        await _data.CaptureAsync(creator.CreatorId, page, existing);
        await _data.CaptureAsync(creator.CreatorId, page, unsubscribed);
        await UnsubscribeAsync(creator.CreatorId, unsubscribed);
        return (creator, existing, unsubscribed);
    }

    [Fact]
    public async Task EveryCategoryInOneFile_IsCountedAndOnlyNewContactsAreCreated()
    {
        var (creator, existing, unsubscribed) = await CreatorAsync();
        var neverContact = Unique("optout");
        await UnsubscribeAsync(creator.CreatorId, neverContact);
        var (ana, ben) = (Unique("ana"), Unique("ben"));
        var before = (await ContactsAsync(creator.CreatorId)).Single(c => c.Email == existing);
        var csv = string.Join("\n",
            "name,email",
            $"Ana,{ana}",
            $"Ben,\"  {ben.ToUpperInvariant()} \"",
            $"Ana again,{ana.ToUpperInvariant()}",
            $"Old friend,{existing}",
            $"Left,{unsubscribed}",
            $"Never,{neverContact}",
            "Blank,",
            "Typo,ana-at-example.com",
            $"Long,{new string('x', 250)}@example.com");

        var summary = await ImportAsync(creator, csv);

        Assert.Equal(
            (false, 9, 6, 2, 1, 1, 2, 3),
            (summary.DryRun, summary.Rows, summary.Valid, summary.New, summary.Existing, summary.DuplicatesInFile, summary.Unsubscribed, summary.InvalidCount));
        Assert.Equal(
            [(8, "", "EMPTY"), (9, "ana-at-example.com", "INVALID_EMAIL"), (10, (new string('x', 250) + "@example.com")[..254], "TOO_LONG")],
            summary.Invalid.Select(i => (i.Row, i.Value, i.Reason)));
        Assert.Equal(TestData.FreePlanContactLimit - 3, summary.RemainingContactLimit);

        var contacts = await ContactsAsync(creator.CreatorId);
        var import = Assert.Single(await ImportsAsync(creator.CreatorId));
        var imported = contacts.Where(c => c.ImportId is not null).ToList();
        Assert.Equal(new[] { ana, ben }.Order(), imported.Select(c => c.Email));
        Assert.All(imported, c =>
        {
            Assert.Equal(import.Id, c.ImportId);
            Assert.Equal(c.ImportedAt, c.FirstCapturedAt);
            Assert.Equal(c.ImportedAt, c.LastCapturedAt);
            Assert.Empty(c.SourceLandingPageIds);
        });
        var after = contacts.Single(c => c.Email == existing);
        Assert.Equal((before.FirstCapturedAt, before.LastCapturedAt, (int?)null), (after.FirstCapturedAt, after.LastCapturedAt, after.ImportId));
        Assert.DoesNotContain(contacts, c => c.Email == neverContact);
        Assert.True(await _data.UnsubscribeExistsAsync(creator.CreatorId, neverContact));
        Assert.True(await _data.UnsubscribeExistsAsync(creator.CreatorId, unsubscribed));

        Assert.Equal(
            (9, 2, 1, 1, 2, 3, ContactImport.CurrentConsentVersion, creator.OwnerUserId, "contacts.csv"),
            (import.RowCount, import.ImportedCount, import.ExistingCount, import.DuplicateCount, import.UnsubscribedCount,
             import.InvalidCount, import.ConsentVersion, import.UserId, import.FileName));
        Assert.Equal(3, await _data.GetContactsUsageAsync(creator.CreatorId));
        Assert.Equal(3, await CountedContactsAsync(creator.CreatorId));
    }

    [Fact]
    public async Task ASemicolonFileWithABom_AndASingleColumnWithoutHeader_BothImport()
    {
        var creator = await _data.CreateCreatorAsync();

        var semicolon = await ImportAsync(creator, $"Name;E-mail\r\nAna;{Unique("a")}\r\nBen;{Unique("b")}\r\n", bom: true);
        var single = await ImportAsync(creator, $"{Unique("c")}\n{Unique("d")}\n{Unique("e")}\n");

        Assert.Equal((2, 3), (semicolon.New, single.New));
        Assert.Equal(5, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task ADryRun_WritesNothing_AndNeedsNoConsent()
    {
        var (creator, existing, _) = await CreatorAsync();
        var usage = await _data.GetContactsUsageAsync(creator.CreatorId);
        var contactsBefore = (await ContactsAsync(creator.CreatorId)).Count;

        var summary = await ImportAsync(creator, $"email\n{Unique("a")}\n{Unique("b")}\n{existing}", dryRun: true, consent: false);

        Assert.Equal((true, 2, 1), (summary.DryRun, summary.New, summary.Existing));
        Assert.Equal(TestData.FreePlanContactLimit - usage - 2, summary.RemainingContactLimit);
        Assert.Equal(contactsBefore, (await ContactsAsync(creator.CreatorId)).Count);
        Assert.Empty(await ImportsAsync(creator.CreatorId));
        Assert.Equal(usage, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task TheLimit_IsAllOrNothing_AndTheDryRunShowsItFirst()
    {
        var creator = await _data.CreateCreatorAsync();
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit - 2);
        var tooMany = $"email\n{Unique("a")}\n{Unique("b")}\n{Unique("c")}";

        var preview = await ImportAsync(creator, tooMany, dryRun: true);
        var refused = await Assert.ThrowsAsync<ConflictException>(() => ImportAsync(creator, tooMany));

        Assert.Equal(-1, preview.RemainingContactLimit);
        Assert.Equal(ContactImportService.ContactLimitExceededCode, refused.Code);
        Assert.Equal(new ContactImportService.ContactLimitDetails(3, 2), refused.Details);
        Assert.Empty(await ContactsAsync(creator.CreatorId));
        Assert.Empty(await ImportsAsync(creator.CreatorId));
        Assert.Equal(TestData.FreePlanContactLimit - 2, await _data.GetContactsUsageAsync(creator.CreatorId));

        var fits = await ImportAsync(creator, $"email\n{Unique("d")}\n{Unique("e")}");

        Assert.Equal((2, 0), (fits.New, fits.RemainingContactLimit));
        Assert.Equal(TestData.FreePlanContactLimit, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task ExistingAndUnsubscribedEmails_DoNotNeedRoomUnderTheLimit()
    {
        var (creator, existing, unsubscribed) = await CreatorAsync();
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit);

        var summary = await ImportAsync(creator, $"email\n{existing}\n{unsubscribed}");

        Assert.Equal((0, 1, 1, 0), (summary.New, summary.Existing, summary.Unsubscribed, summary.RemainingContactLimit));
    }

    [Fact]
    public async Task TheCounter_FollowsTheContacts_AcrossImportUnsubscribeAndDelete()
    {
        var creator = await _data.CreateCreatorAsync();
        var emails = Enumerable.Range(0, 25).Select(i => Unique($"bulk{i}")).ToList();

        await ImportAsync(creator, "email\n" + string.Join("\n", emails));
        await UnsubscribeAsync(creator.CreatorId, emails[0]);
        await using (var db = _fixture.CreateDbContext())
            await new ContactsService(new MarketingCreatorContextProvider(db), new ContactsRepository(db), new MarketingUnitOfWork(db),
                    new CreatorUsageService(db), new UnsubscribeRepository(db))
                .DeleteAsync(creator.Slug, creator.OwnerUserId, emails[1], None);

        Assert.Equal(23, await _data.GetContactsUsageAsync(creator.CreatorId));
        Assert.Equal(23, await CountedContactsAsync(creator.CreatorId));
    }

    [Fact]
    public async Task WithoutConsent_ARealImportIsRefused()
    {
        var creator = await _data.CreateCreatorAsync();

        var refused = await Assert.ThrowsAsync<BadRequestException>(() => ImportAsync(creator, $"email\n{Unique("a")}", consent: false));

        Assert.Equal(ContactImportErrors.ConsentRequired, refused.Code);
        Assert.Empty(await ContactsAsync(creator.CreatorId));
    }

    [Fact]
    public async Task AnotherCreatorsSlug_Is404()
    {
        var owner = await _data.CreateCreatorAsync();
        var intruder = await _data.CreateCreatorAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => ImportAsync(owner with { OwnerUserId = intruder.OwnerUserId }, $"email\n{Unique("a")}"));
        Assert.Empty(await ContactsAsync(owner.CreatorId));
    }

    [Fact]
    public async Task AFileWithOnlyAHeader_IsEmpty()
    {
        var creator = await _data.CreateCreatorAsync();

        var refused = await Assert.ThrowsAsync<BadRequestException>(() => ImportAsync(creator, "name,email\n"));

        Assert.Equal(ContactImportErrors.FileEmpty, refused.Code);
    }

    [Fact]
    public async Task ImportedContacts_ShowUpInTheDirectoryStatsAndTheAllAudience()
    {
        var (creator, _, _) = await CreatorAsync();
        var ana = Unique("ana");

        await ImportAsync(creator, $"email\n{ana}\n{Unique("ben")}");

        await using var db = _fixture.CreateDbContext();
        var contacts = new ContactsService(new MarketingCreatorContextProvider(db), new ContactsRepository(db), new MarketingUnitOfWork(db),
            new CreatorUsageService(db), new UnsubscribeRepository(db));
        var stats = await contacts.GetStatsAsync(creator.Slug, creator.OwnerUserId, None);
        var page = await contacts.SearchAsync(creator.Slug, creator.OwnerUserId, ana, null, null, null, 10, None);
        var allAudience = await new AudienceService(db).GetAudienceCountAsync(CampaignAudienceType.All, null, null, null, creator.CreatorId, None);

        Assert.Equal((2, 2, 4, 3), (stats.ImportedThisMonth, stats.Imported, stats.NewThisMonth, stats.Active));
        var row = Assert.Single(page.Contacts);
        Assert.NotNull(row.ImportedAt);
        Assert.Empty(row.SourceList);
        Assert.Equal(3, allAudience);
    }
}
