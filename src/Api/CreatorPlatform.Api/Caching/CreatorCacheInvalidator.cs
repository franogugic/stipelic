using CreatorPlatform.Analytics.Infrastructure.Services;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Orders.Infrastructure.Services;
using CreatorPlatform.Shared.Infrastructure.Redis;

namespace CreatorPlatform.Api.Caching;

/// <summary>The per-creator caches that hold plan-dependent, workspace-wide or timezone-bucketed numbers, dropped in
/// one Redis round trip (a batch: delete both summaries, bump the trends generation). The other short-lived one
/// (payout schedule: 60 s) expires on its own.</summary>
public sealed class CreatorCacheInvalidator : ICreatorCacheInvalidator
{
    private readonly RedisJsonCache _cache;

    public CreatorCacheInvalidator(RedisJsonCache cache)
    {
        _cache = cache;
    }

    public Task InvalidateAsync(int creatorId, CancellationToken ct) =>
        _cache.InvalidateAsync(
            $"the cached analytics of creator {creatorId}",
            async database =>
            {
                var batch = database.CreateBatch();
                var summaries = batch.KeyDeleteAsync([HomeSummaryCache.Key(creatorId), ViewsSummaryCache.Key(creatorId)]);
                var trends = DashboardTrendsCache.BumpGenerationAsync(batch, creatorId);
                batch.Execute();
                await Task.WhenAll(summaries, trends);
                return true;
            },
            ct);
}
