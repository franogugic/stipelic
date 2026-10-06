using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Domain.PageViews;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Analytics.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Caching.Memory;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>The views summary cache is only touched after the ownership check and is keyed by creator id, so another
/// user's request can neither read nor poison the owner's entry.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ViewsSummaryCacheOwnershipTests : IDisposable
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    // One process-wide cache, as in production (singleton over IMemoryCache); services are per request.
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly ViewsSummaryCache _cache;

    public ViewsSummaryCacheOwnershipTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
        _cache = new ViewsSummaryCache(_memoryCache);
    }

    public void Dispose() => _memoryCache.Dispose();

    private async Task<List<LandingPageViewsSummaryDto>> GetAsync(string slug, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new PageViewService(new PageViewRepository(db), _cache, new CreatorContextProvider(db));
        return await service.GetViewsSummaryByCreatorAsync(slug, userId, CancellationToken.None);
    }

    private async Task InvalidateAsync(string slug, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new PageViewService(new PageViewRepository(db), _cache, new CreatorContextProvider(db));
        await service.InvalidateViewsSummaryAsync(slug, userId);
    }

    private async Task RecordViewAsync(int landingPageId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await new PageViewRepository(db).AddAsync(new PageView
        {
            Id = Guid.NewGuid(),
            LandingPageId = landingPageId,
            VisitorId = Guid.NewGuid(),
            ViewedAt = now,
            ViewedDate = DateOnly.FromDateTime(now.UtcDateTime)
        }, CancellationToken.None);
    }

    private static long TotalViews(List<LandingPageViewsSummaryDto> summary) => Assert.Single(summary).TotalViews;

    [Fact]
    public async Task NonOwner_Gets404_WhileTheOwnersSummaryIsCached()
    {
        var owner = await _data.CreateCreatorAsync();
        await RecordViewAsync(await _data.CreateLandingPageAsync(owner.CreatorId));
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
        await RecordViewAsync(await _data.CreateLandingPageAsync(owner.CreatorId));
        var intruderId = await _data.CreateUserAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(owner.Slug, intruderId));

        Assert.False(_cache.TryGet(owner.CreatorId, out _));
        Assert.Equal(1, TotalViews(await GetAsync(owner.Slug, owner.OwnerUserId)));
    }

    [Fact]
    public async Task NonOwnerRequest_DoesNotOverwriteOrDropTheOwnersEntry()
    {
        var owner = await _data.CreateCreatorAsync();
        await RecordViewAsync(await _data.CreateLandingPageAsync(owner.CreatorId));
        var intruder = await _data.CreateCreatorAsync();
        var ownersSummary = await GetAsync(owner.Slug, owner.OwnerUserId);

        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(owner.Slug, intruder.OwnerUserId));
        await InvalidateAsync(owner.Slug, intruder.OwnerUserId);
        // The intruder's own workspace (no pages) is cached under its own id, not under the owner's.
        Assert.Empty(await GetAsync(intruder.Slug, intruder.OwnerUserId));

        Assert.Same(ownersSummary, await GetAsync(owner.Slug, owner.OwnerUserId));
        Assert.Equal(1, TotalViews(ownersSummary));
    }

    [Fact]
    public async Task Owner_GetsCacheHits_UntilInvalidated()
    {
        var owner = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(owner.CreatorId);
        await RecordViewAsync(page);
        var first = await GetAsync(owner.Slug, owner.OwnerUserId);
        await RecordViewAsync(page);

        var second = await GetAsync(owner.Slug, owner.OwnerUserId);

        Assert.Same(first, second);
        Assert.Equal(1, TotalViews(second));

        await InvalidateAsync(owner.Slug, owner.OwnerUserId);

        Assert.Equal(2, TotalViews(await GetAsync(owner.Slug, owner.OwnerUserId)));
    }
}
