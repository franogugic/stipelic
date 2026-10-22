using CreatorPlatform.Analytics.Infrastructure.Services;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Orders.Infrastructure.Services;
using CreatorPlatform.Shared.Infrastructure.Redis;

namespace CreatorPlatform.Api.Caching;

/// <summary>The per-creator caches that hold plan-dependent, workspace-wide or timezone-bucketed numbers, dropped in
/// one Redis round trip (a batch: delete each summary, bump the trends generation). The other short-lived one
/// (payout schedule: 60 s) expires on its own.</summary>
/// <remarks>One key per command: on a clustered Redis the keys live in different slots, and a multi-key DEL across
/// slots fails with CROSSSLOT. A batch is a pipeline, not a transaction, so its commands may go to different nodes.</remarks>
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
                var homeSummary = batch.KeyDeleteAsync(HomeSummaryCache.Key(creatorId));
                var viewsSummary = batch.KeyDeleteAsync(ViewsSummaryCache.Key(creatorId));
                var trends = DashboardTrendsCache.BumpGenerationAsync(batch, creatorId);
                batch.Execute();
                await Task.WhenAll(homeSummary, viewsSummary, trends);
                return true;
            },
            ct);
}
