using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>max_contacts counts unique contacts (contact_summaries rows), not captures.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ContactLimitTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public ContactLimitTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    [Fact]
    public async Task FirstCapture_CountsOne()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);

        await _data.CaptureAsync(creator.CreatorId, page, TestData.UniqueEmail());

        Assert.Equal(1, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task SameEmailOnASecondPage_CountsZeroAndIsNotBlockedAtTheLimit()
    {
        var creator = await _data.CreateCreatorAsync();
        var pageA = await _data.CreateLandingPageAsync(creator.CreatorId);
        var pageB = await _data.CreateLandingPageAsync(creator.CreatorId);
        var email = TestData.UniqueEmail();
        await _data.CaptureAsync(creator.CreatorId, pageA, email);
        Assert.Equal(1, await _data.GetContactsUsageAsync(creator.CreatorId));
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit);

        await _data.CaptureAsync(creator.CreatorId, pageB, email);

        Assert.Equal(2, await _data.CountCapturesAsync(creator.CreatorId, email));
        Assert.Equal(TestData.FreePlanContactLimit, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task NewEmailAtTheLimit_IsBlockedAndLeavesNothingBehind()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit);
        var email = TestData.UniqueEmail();

        await Assert.ThrowsAsync<ConflictException>(() => _data.CaptureAsync(creator.CreatorId, page, email));

        Assert.Equal(0, await _data.CountCapturesAsync(creator.CreatorId, email));
        Assert.False(await _data.SummaryExistsAsync(creator.CreatorId, email));
        Assert.Equal(TestData.FreePlanContactLimit, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task NewEmailOneBelowTheLimit_IsAcceptedAndFillsTheLastSlot()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit - 1);
        var email = TestData.UniqueEmail();

        await _data.CaptureAsync(creator.CreatorId, page, email);

        Assert.True(await _data.SummaryExistsAsync(creator.CreatorId, email));
        Assert.Equal(TestData.FreePlanContactLimit, await _data.GetContactsUsageAsync(creator.CreatorId));
    }

    [Fact]
    public async Task MigrationRecalculation_SetsEveryCounterToItsContactCountAndIsIdempotent()
    {
        // Over-counted (old per-capture behaviour) plus a stray legacy row with a literal 0001-01-01 start.
        var overCounted = await _data.CreateCreatorAsync();
        var pageA = await _data.CreateLandingPageAsync(overCounted.CreatorId);
        var pageB = await _data.CreateLandingPageAsync(overCounted.CreatorId);
        var shared = TestData.UniqueEmail();
        await _data.CaptureAsync(overCounted.CreatorId, pageA, shared);
        await _data.CaptureAsync(overCounted.CreatorId, pageB, shared);
        await _data.CaptureAsync(overCounted.CreatorId, pageA, TestData.UniqueEmail());
        await _data.SetContactsUsageAsync(overCounted.CreatorId, 3);
        await InsertLegacyLiteralMinCounterAsync(overCounted.CreatorId, 7);

        // Contacts but no counter at all.
        var uncounted = await _data.CreateCreatorAsync();
        var uncountedPage = await _data.CreateLandingPageAsync(uncounted.CreatorId);
        await _data.CaptureAsync(uncounted.CreatorId, uncountedPage, TestData.UniqueEmail());
        await DeleteContactsCountersAsync(uncounted.CreatorId);

        // A counter but no contacts left.
        var empty = await _data.CreateCreatorAsync();
        await _data.SetContactsUsageAsync(empty.CreatorId, 5);

        for (var run = 0; run < 2; run++)
        {
            await using var db = _fixture.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync(RecountContactsTowardContactLimit.RecalculateContactsUsageSql);

            Assert.Equal(2, await _data.GetContactsUsageAsync(overCounted.CreatorId));
            Assert.Equal(1, await _data.CountContactsUsageRowsAsync(overCounted.CreatorId));
            Assert.Equal(1, await _data.GetContactsUsageAsync(uncounted.CreatorId));
            Assert.Equal(0, await _data.GetContactsUsageAsync(empty.CreatorId));
            Assert.Equal(0, await _data.CountContactsUsageRowsAsync(empty.CreatorId));
        }

        // The app keeps counting on the recalculated row: one new contact makes it 3, still a single row.
        await _data.CaptureAsync(overCounted.CreatorId, pageB, TestData.UniqueEmail());
        Assert.Equal(3, await _data.GetContactsUsageAsync(overCounted.CreatorId));
        Assert.Equal(1, await _data.CountContactsUsageRowsAsync(overCounted.CreatorId));
    }

    private async Task InsertLegacyLiteralMinCounterAsync(int creatorId, int usedValue)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_usage_counters
                ("CreatorId", "UsageKey", "UsedValue", "PeriodStart", "PeriodEnd", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, 'max_contacts', {usedValue},
                    TIMESTAMPTZ '0001-01-01 00:00:00+00', TIMESTAMPTZ '9999-12-31 00:00:00+00', now(), now())
            """);
    }

    private async Task DeleteContactsCountersAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM creators.creator_usage_counters WHERE "CreatorId" = {creatorId} AND "UsageKey" = 'max_contacts'
            """);
    }
}
