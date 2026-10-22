using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>The home summary cache is only touched after the ownership check and is keyed by creator id, so another
/// user's request can neither read nor poison the owner's entry.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HomeSummaryCacheOwnershipTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    // One process-wide cache, as in production (a singleton over the shared Redis); services are per request.
    private readonly TestRedisClient _redis;
    private readonly HomeSummaryCache _cache;
    private readonly DashboardTrendsCache _trendsCache;

    public HomeSummaryCacheOwnershipTests(PostgresFixture fixture, RedisFixture redis)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
        _redis = RedisFixture.CreateClient(redis.CreateOptions());
        _cache = new HomeSummaryCache(_redis.JsonCache);
        _trendsCache = new DashboardTrendsCache(_redis.JsonCache);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    private async Task<bool> IsCachedAsync(int creatorId) =>
        await _cache.GetAsync(creatorId, CancellationToken.None) is not null;

    private async Task<HomeSummaryDto> GetAsync(string slug, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new OrderService(new OrderRepository(db), _cache, _trendsCache);
        return await service.GetHomeSummaryAsync(slug, userId, CancellationToken.None);
    }

    private async Task InvalidateAsync(string slug, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new OrderService(new OrderRepository(db), _cache, _trendsCache);
        await service.InvalidateHomeSummaryAsync(slug, userId);
    }

    [Fact]
    public async Task NonOwner_Gets404_WhileTheOwnersSummaryIsCached()
    {
        var owner = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(owner.CreatorId);
        var intruderId = await _data.CreateUserAsync();
        await GetAsync(owner.Slug, owner.OwnerUserId);
        Assert.True(await IsCachedAsync(owner.CreatorId));

        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(owner.Slug, intruderId));
        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync($"missing-{Guid.NewGuid():N}", intruderId));
    }

    [Fact]
    public async Task NonOwnerRequestFirst_CreatesNoEntry_AndTheOwnerGetsRealNumbers()
    {
        var owner = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(owner.CreatorId);
        var intruderId = await _data.CreateUserAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(owner.Slug, intruderId));

        Assert.False(await IsCachedAsync(owner.CreatorId));
        Assert.Equal(1, (await GetAsync(owner.Slug, owner.OwnerUserId)).LandingPageCount);
    }

    [Fact]
    public async Task NonOwnerRequest_DoesNotOverwriteOrDropTheOwnersEntry()
    {
        var owner = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(owner.CreatorId);
        var intruder = await _data.CreateCreatorAsync();
        await GetAsync(owner.Slug, owner.OwnerUserId);
        // Only a cache hit can still say 1 after this.
        await _data.CreateLandingPageAsync(owner.CreatorId);

        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(owner.Slug, intruder.OwnerUserId));
        await InvalidateAsync(owner.Slug, intruder.OwnerUserId);
        // The intruder's own workspace is cached under its own id, not under the owner's.
        Assert.Equal(0, (await GetAsync(intruder.Slug, intruder.OwnerUserId)).LandingPageCount);

        Assert.Equal(1, (await GetAsync(owner.Slug, owner.OwnerUserId)).LandingPageCount);
    }

    [Fact]
    public async Task Owner_GetsCacheHits_UntilInvalidated()
    {
        var owner = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(owner.CreatorId);
        var first = await GetAsync(owner.Slug, owner.OwnerUserId);
        await _data.CreateLandingPageAsync(owner.CreatorId);

        var second = await GetAsync(owner.Slug, owner.OwnerUserId);

        Assert.Equal(1, first.LandingPageCount);
        Assert.Equal(1, second.LandingPageCount);

        await InvalidateAsync(owner.Slug, owner.OwnerUserId);

        Assert.Equal(2, (await GetAsync(owner.Slug, owner.OwnerUserId)).LandingPageCount);
    }
}
