using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Marketing;

/// <summary>The subscribers list's status filter, and joined / unsubscribed per month in the creator's timezone.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ContactStatusAndMonthlyTests
{
    private const string Zagreb = "Europe/Zagreb";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public ContactStatusAndMonthlyTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<T> WithServiceAsync<T>(Func<ContactsService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(new ContactsService(
            new MarketingCreatorContextProvider(db), new ContactsRepository(db), new MarketingUnitOfWork(db), new CreatorUsageService(db), new UnsubscribeRepository(db)));
    }

    private Task<List<string>> ListAsync(TestData.SeededCreator creator, string? status) =>
        WithServiceAsync(async s => (await s.SearchAsync(creator.Slug, creator.OwnerUserId, null, null, status, null, 50, CancellationToken.None))
            .Contacts.Select(c => c.Email).ToList());

    [Fact]
    public async Task TheStatusFilter_SplitsActiveAndUnsubscribed()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        var now = DateTimeOffset.UtcNow;
        await AddContactAsync(creator.CreatorId, "a@example.test", now);
        await AddContactAsync(creator.CreatorId, "b@example.test", now);
        await AddContactAsync(creator.CreatorId, "c@example.test", now);
        await AddUnsubscribeAsync(creator.CreatorId, "b@example.test", now);
        await AddUnsubscribeAsync(other.CreatorId, "c@example.test", now); // another creator's opt-out

        Assert.Equal(["a@example.test", "b@example.test", "c@example.test"], await ListAsync(creator, null));
        Assert.Equal(["a@example.test", "c@example.test"], await ListAsync(creator, "active"));
        Assert.Equal(["b@example.test"], await ListAsync(creator, "UNSUBSCRIBED"));
        await Assert.ThrowsAsync<BadRequestException>(() => ListAsync(creator, "bounced"));
    }

    [Fact]
    public async Task Monthly_CountsJoinedAndUnsubscribedPerCreatorTimezoneMonth()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        var now = DateTimeOffset.UtcNow;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);
        // 23:30 UTC on the last day of a UTC month: the 1st of the next month in Zagreb.
        var lateNight = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1).AddMinutes(-30);
        var localMonth = TimeZoneInfo.ConvertTime(lateNight, zone).ToString("yyyy-MM");
        var currentMonth = TimeZoneInfo.ConvertTime(now, zone).ToString("yyyy-MM");

        await AddContactAsync(creator.CreatorId, "late@example.test", lateNight);
        await AddContactAsync(creator.CreatorId, "now1@example.test", now);
        await AddContactAsync(creator.CreatorId, "now2@example.test", now);
        await AddContactAsync(creator.CreatorId, "old@example.test", now.AddYears(-2)); // outside the window
        await AddUnsubscribeAsync(creator.CreatorId, "late@example.test", lateNight);
        await AddUnsubscribeAsync(creator.CreatorId, "now1@example.test", now);
        await AddContactAsync(other.CreatorId, "someone@example.test", now);

        var monthly = (await WithServiceAsync(s => s.GetStatsAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None))).Monthly;

        Assert.Equal(12, monthly.Count);
        Assert.Equal(currentMonth, monthly[^1].Month);
        Assert.Equal((1, 1), (monthly.Single(m => m.Month == localMonth).Joined, monthly.Single(m => m.Month == localMonth).Unsubscribed));
        Assert.Equal((2, 1), (monthly[^1].Joined, monthly[^1].Unsubscribed));
        Assert.Equal((3, 2), (monthly.Sum(m => m.Joined), monthly.Sum(m => m.Unsubscribed)));
    }

    private static DateTimeOffset LocalNoon(int days)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date.AddDays(days).AddHours(12);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    [Fact]
    public async Task APeriod_GivesJoinedUnsubscribedAndTheRunningTotalPerBucket()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        await AddContactAsync(creator.CreatorId, "base1@example.test", LocalNoon(-100));
        await AddContactAsync(creator.CreatorId, "base2@example.test", LocalNoon(-40));
        await AddContactAsync(creator.CreatorId, "a@example.test", LocalNoon(-5));
        await AddContactAsync(creator.CreatorId, "b@example.test", LocalNoon(-5));
        await AddContactAsync(creator.CreatorId, "c@example.test", LocalNoon(0));
        await AddUnsubscribeAsync(creator.CreatorId, "a@example.test", LocalNoon(-1));
        await AddContactAsync(other.CreatorId, "z@example.test", LocalNoon(0));

        var stats = await WithServiceAsync(s => s.GetPeriodStatsAsync(creator.Slug, creator.OwnerUserId, "30d", CancellationToken.None));

        Assert.Equal(("30d", "day", 30), (stats.Period, stats.Step, stats.Buckets.Count));
        Assert.Equal((5, 4, 1), (stats.Total, stats.Active, stats.Unsubscribed)); // the all-time headline is unchanged
        Assert.Equal((3, 1), (stats.Buckets.Sum(b => b.Joined), stats.Buckets.Sum(b => b.Unsubscribed)));
        Assert.Equal((2, 0, 4), (stats.Buckets[^6].Joined, stats.Buckets[^6].Unsubscribed, stats.Buckets[^6].Total));
        Assert.Equal((0, 1), (stats.Buckets[^2].Joined, stats.Buckets[^2].Unsubscribed));
        Assert.Equal((1, 0, 5), (stats.Buckets[^1].Joined, stats.Buckets[^1].Unsubscribed, stats.Buckets[^1].Total));
        Assert.Equal(2, stats.Buckets[0].Total); // base1 and base2 were already there when the window opened
    }

    [Fact]
    public async Task ThreeMonths_AreWeeksOfCalendarMonthsBack_AndPeriodsAreValidated()
    {
        var creator = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        await AddContactAsync(creator.CreatorId, "a@example.test", LocalNoon(-30));

        var stats = await WithServiceAsync(s => s.GetPeriodStatsAsync(creator.Slug, creator.OwnerUserId, "3m", CancellationToken.None));

        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(Zagreb)).Date;
        var days = (today - today.AddMonths(-3).AddDays(1)).Days + 1;           // 3m: the day after the same date 3 months ago
        Assert.Equal(("week", (days + 6) / 7), (stats.Step, stats.Buckets.Count));
        Assert.Equal(1, stats.Buckets.Sum(b => b.Joined));
        Assert.Equal(1, stats.Buckets[^1].Total);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            WithServiceAsync(s => s.GetPeriodStatsAsync(creator.Slug, creator.OwnerUserId, "90d", CancellationToken.None)));
        var other = await _data.CreateCreatorAsync();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            WithServiceAsync(s => s.GetPeriodStatsAsync(creator.Slug, other.OwnerUserId, "7d", CancellationToken.None)));
    }

    [Fact]
    public async Task Stats_CountUnsubscribedAmongExistingContactsOnly()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        var now = DateTimeOffset.UtcNow;
        await AddContactAsync(creator.CreatorId, "a@example.test", now);
        await AddContactAsync(creator.CreatorId, "b@example.test", now);
        await AddContactAsync(creator.CreatorId, "c@example.test", now);
        await AddContactAsync(creator.CreatorId, "gone@example.test", now);
        await AddUnsubscribeAsync(creator.CreatorId, "b@example.test", now);
        await AddUnsubscribeAsync(creator.CreatorId, "gone@example.test", now);
        await AddUnsubscribeAsync(creator.CreatorId, "never-subscribed@example.test", now); // not a contact
        await AddUnsubscribeAsync(other.CreatorId, "c@example.test", now); // another creator's opt-out
        await WithServiceAsync(async s =>
        {
            await s.DeleteAsync(creator.Slug, creator.OwnerUserId, "gone@example.test", CancellationToken.None);
            return 0;
        });

        var stats = await WithServiceAsync(s => s.GetStatsAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None));

        Assert.Equal((3, 2, 1), (stats.Total, stats.Active, stats.Unsubscribed));
        Assert.Equal(stats.Total, stats.Active + stats.Unsubscribed);
        // The tile matches the status filter's list.
        Assert.Single(await ListAsync(creator, "unsubscribed"));
        // The per-month series is event-based: all three opt-outs of this creator are still in it.
        Assert.Equal(3, stats.Monthly.Sum(m => m.Unsubscribed));
    }

    // ---- R6.6: the period's start, previous window, imports and where people joined ---------------------------------

    private async Task<int> AddPageAsync(int creatorId, string title)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {title}, {$"p-{Guid.NewGuid():N}"}, 'LeadGen', 'Published', now(), now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task<int> AddImportAsync(int creatorId, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO marketing.contact_imports ("PublicId", "CreatorId", "UserId", "FileName", "RowCount", "ImportedCount", "ExistingCount",
                                                   "DuplicateCount", "UnsubscribedCount", "InvalidCount", "ConsentVersion", "ConsentAttestedAt", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {userId}, 'list.csv', 2, 2, 0, 0, 0, 0, 'contacts-import-v1', now(), now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    /// <summary>A contact whose capture history is <paramref name="pages"/> (first source first), or one an import created.</summary>
    private async Task AddSourcedContactAsync(int creatorId, string email, DateTimeOffset at, int[] pages, int? importId = null)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.contact_summaries ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds", "ImportId", "ImportedAt")
            VALUES ({creatorId}, {email}, {at}, {at}, {pages}, {importId}, {(importId is null ? null : at)})
            """);
    }

    [Fact]
    public async Task APeriod_HasItsStart_ItsJoinedUnsubscribedAndImported_AndThePreviousWindow()
    {
        var creator = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        var page = await AddPageAsync(creator.CreatorId, "Guide");
        var import = await AddImportAsync(creator.CreatorId, creator.OwnerUserId);
        await AddSourcedContactAsync(creator.CreatorId, "old@example.test", LocalNoon(-40), [page]);         // before both windows
        await AddSourcedContactAsync(creator.CreatorId, "prev@example.test", LocalNoon(-10), [page]);        // the previous 7 days
        await AddSourcedContactAsync(creator.CreatorId, "prev-imp@example.test", LocalNoon(-9), [], import);
        await AddSourcedContactAsync(creator.CreatorId, "a@example.test", LocalNoon(-2), [page]);
        await AddSourcedContactAsync(creator.CreatorId, "b@example.test", LocalNoon(-1), [], import);
        await AddSourcedContactAsync(creator.CreatorId, "c@example.test", LocalNoon(0), [], import);
        await AddUnsubscribeAsync(creator.CreatorId, "old@example.test", LocalNoon(-1));
        await AddUnsubscribeAsync(creator.CreatorId, "prev@example.test", LocalNoon(-8));
        await AddUnsubscribeAsync(creator.CreatorId, "gone@example.test", LocalNoon(-3));                    // a deleted contact's opt-out

        var stats = await WithServiceAsync(s => s.GetPeriodStatsAsync(creator.Slug, creator.OwnerUserId, "7d", CancellationToken.None));

        Assert.Equal(3, stats.StartTotal);                                                 // old, prev, prev-imp
        Assert.Equal((3, 2, 2), (stats.Joined, stats.UnsubscribedInPeriod, stats.ImportedInPeriod));
        Assert.Equal(new ContactPeriodPreviousDto(2, 1, 1), stats.Previous);
        Assert.Equal(stats.Range.From, stats.PreviousRange!.To);
        Assert.Equal((stats.Joined, stats.ImportedInPeriod, stats.UnsubscribedInPeriod),
            (stats.Buckets.Sum(b => b.Joined), stats.Buckets.Sum(b => b.Imported), stats.Buckets.Sum(b => b.Unsubscribed)));
        Assert.Equal(1, stats.Buckets[^1].Imported);                                       // c, today
        Assert.Equal(stats.StartTotal + stats.Joined, stats.Buckets[^1].Total);

        var all = await WithServiceAsync(s => s.GetPeriodStatsAsync(creator.Slug, creator.OwnerUserId, "all", CancellationToken.None));
        Assert.Equal((0, 6, (ContactPeriodPreviousDto?)null), (all.StartTotal, all.Joined, all.Previous));
        Assert.Null(all.PreviousRange);
    }

    [Fact]
    public async Task WhereTheyJoined_CountsEachContactOnceAtItsFirstSource_ImportIncluded_AddingUpToJoined()
    {
        var creator = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        var guide = await AddPageAsync(creator.CreatorId, "Guide");
        var course = await AddPageAsync(creator.CreatorId, "Course");
        var import = await AddImportAsync(creator.CreatorId, creator.OwnerUserId);
        await AddSourcedContactAsync(creator.CreatorId, "a@example.test", LocalNoon(-1), [guide, course]);   // first came through the guide
        await AddSourcedContactAsync(creator.CreatorId, "b@example.test", LocalNoon(-2), [course, guide]);
        await AddSourcedContactAsync(creator.CreatorId, "c@example.test", LocalNoon(-3), [course]);
        await AddSourcedContactAsync(creator.CreatorId, "d@example.test", LocalNoon(-1), [guide], import);   // imported, then signed up
        await AddSourcedContactAsync(creator.CreatorId, "e@example.test", LocalNoon(-1), [], import);
        await AddSourcedContactAsync(creator.CreatorId, "f@example.test", LocalNoon(-1), [], import);
        await AddSourcedContactAsync(creator.CreatorId, "old@example.test", LocalNoon(-30), [guide]);         // before the 7 days

        var stats = await WithServiceAsync(s => s.GetPeriodStatsAsync(creator.Slug, creator.OwnerUserId, "7d", CancellationToken.None));

        Assert.Equal(
            [("import", "Import", 3), ("page", "Course", 2), ("page", "Guide", 1)],
            stats.Sources.Select(x => (x.Kind, x.Title, x.Count)));
        Assert.Null(stats.Sources[0].LandingPagePublicId);
        Assert.NotNull(stats.Sources[1].LandingPagePublicId);
        Assert.Equal(stats.Joined, stats.Sources.Sum(x => x.Count));

        var allTime = await WithServiceAsync(s => s.GetStatsAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None));
        Assert.Equal(4, allTime.Sources.Single(x => x.Title == "Guide").Count);   // the call without a period still counts every page a contact has
    }

    private async Task SetTimezoneAsync(int creatorId, string timezone)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, 'Brand', '#E2553A', {timezone}, 'en', {now}, {now})
            """);
    }

    private async Task AddContactAsync(int creatorId, string email, DateTimeOffset firstCapturedAt)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.contact_summaries ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds")
            VALUES ({creatorId}, {email}, {firstCapturedAt}, {firstCapturedAt}, ARRAY[]::int[])
            """);
    }

    private async Task AddUnsubscribeAsync(int creatorId, string email, DateTimeOffset at)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.unsubscribes ("CreatorId", "Email", "Source", "UnsubscribedAt")
            VALUES ({creatorId}, {email}, 'Link', {at})
            """);
    }
}
