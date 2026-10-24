using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>QA-13: the home summary is cached, but its email usage counter is read live — a campaign sent (API) or a
/// failed send given back (Worker) shows at once, with no cache invalidation on either path.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HomeSummaryEmailCounterTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly TestRedisClient _redis;
    private readonly HomeSummaryCache _cache;

    public HomeSummaryEmailCounterTests(PostgresFixture fixture, RedisFixture redis)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
        _redis = RedisFixture.CreateClient(redis.CreateOptions());
        _cache = new HomeSummaryCache(_redis.JsonCache);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    private async Task<HomeSummaryDto> GetAsync(TestData.SeededCreator creator)
    {
        await using var db = _fixture.CreateDbContext();
        return await new OrderService(new OrderRepository(db), _cache).GetHomeSummaryAsync(creator.Slug, creator.OwnerUserId, default);
    }

    private async Task SetEmailsSentAsync(int creatorId, int used)
    {
        await using var db = _fixture.CreateDbContext();
        var (periodStart, periodEnd) = UsagePeriodResolver.Resolve(UsagePeriod.CalendarMonth, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_usage_counters
                ("CreatorId", "UsageKey", "UsedValue", "PeriodStart", "PeriodEnd", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, 'max_email_sends_per_month', {used}, {periodStart}, {periodEnd}, {now}, {now})
            ON CONFLICT ("CreatorId", "UsageKey", "PeriodStart", "PeriodEnd")
            DO UPDATE SET "UsedValue" = {used}, "UpdatedAt" = {now}
            """);
    }

    [Fact]
    public async Task ACachedSummary_ShowsTheEmailsSentSoFar_AfterASendAndARefund()
    {
        var creator = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(creator.CreatorId);
        await SetEmailsSentAsync(creator.CreatorId, 486);
        var first = await GetAsync(creator);
        Assert.NotNull(await _cache.GetAsync(creator.CreatorId, default));

        await SetEmailsSentAsync(creator.CreatorId, 493);   // a campaign to 7 recipients
        var afterSend = await GetAsync(creator);
        await SetEmailsSentAsync(creator.CreatorId, 492);   // one recipient failed for good: the Worker gives it back
        var afterRefund = await GetAsync(creator);

        Assert.Equal((486, 493, 492), (first.EmailsSentThisMonth, afterSend.EmailsSentThisMonth, afterRefund.EmailsSentThisMonth));
        Assert.Equal(first.LandingPageCount, afterSend.LandingPageCount);   // the rest still comes from the cache
    }
}
