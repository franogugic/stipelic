using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Caching.Memory;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>The home summary cache is only touched after the ownership check and is keyed by creator id, so another
/// user's request can neither read nor poison the owner's entry.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HomeSummaryCacheOwnershipTests : IDisposable
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    // One process-wide cache, as in production (singleton over IMemoryCache); services are per request.
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly HomeSummaryCache _cache;

    public HomeSummaryCacheOwnershipTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
        _cache = new HomeSummaryCache(_memoryCache);
    }

    public void Dispose() => _memoryCache.Dispose();

    private async Task<HomeSummaryDto> GetAsync(string slug, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new OrderService(new OrderRepository(db), _cache, new DashboardTrendsCache(_memoryCache));
        return await service.GetHomeSummaryAsync(slug, userId, CancellationToken.None);
    }

    private async Task InvalidateAsync(string slug, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new OrderService(new OrderRepository(db), _cache, new DashboardTrendsCache(_memoryCache));
        await service.InvalidateHomeSummaryAsync(slug, userId);
    }

    [Fact]
    public async Task NonOwner_Gets404_WhileTheOwnersSummaryIsCached()
    {
        var owner = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(owner.CreatorId);
        var intruderId = await _data.CreateUserAsync();
        await GetAsync(owner.Slug, owner.OwnerUserId);
        Assert.True(_cache.TryGet(owner.CreatorId, out _));

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

        Assert.False(_cache.TryGet(owner.CreatorId, out _));
        Assert.Equal(1, (await GetAsync(owner.Slug, owner.OwnerUserId)).LandingPageCount);
    }

    [Fact]
    public async Task NonOwnerRequest_DoesNotOverwriteOrDropTheOwnersEntry()
    {
        var owner = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(owner.CreatorId);
        var intruder = await _data.CreateCreatorAsync();
        var ownersSummary = await GetAsync(owner.Slug, owner.OwnerUserId);

        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(owner.Slug, intruder.OwnerUserId));
        await InvalidateAsync(owner.Slug, intruder.OwnerUserId);
        // The intruder's own workspace is cached under its own id, not under the owner's.
        await GetAsync(intruder.Slug, intruder.OwnerUserId);

        Assert.Same(ownersSummary, await GetAsync(owner.Slug, owner.OwnerUserId));
        Assert.Equal(1, ownersSummary.LandingPageCount);
    }

    [Fact]
    public async Task Owner_GetsCacheHits_UntilInvalidated()
    {
        var owner = await _data.CreateCreatorAsync();
        await _data.CreateLandingPageAsync(owner.CreatorId);
        var first = await GetAsync(owner.Slug, owner.OwnerUserId);
        await _data.CreateLandingPageAsync(owner.CreatorId);

        var second = await GetAsync(owner.Slug, owner.OwnerUserId);

        Assert.Same(first, second);
        Assert.Equal(1, second.LandingPageCount);

        await InvalidateAsync(owner.Slug, owner.OwnerUserId);

        Assert.Equal(2, (await GetAsync(owner.Slug, owner.OwnerUserId)).LandingPageCount);
    }
}
