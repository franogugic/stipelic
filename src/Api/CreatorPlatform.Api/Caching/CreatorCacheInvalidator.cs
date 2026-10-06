using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Orders.Application.Interfaces;

namespace CreatorPlatform.Api.Caching;

/// <summary>The per-creator caches that hold plan-dependent or workspace-wide numbers. The short-lived ones
/// (dashboard trends, time series, payout schedule: 60 s) expire on their own.</summary>
public sealed class CreatorCacheInvalidator : ICreatorCacheInvalidator
{
    private readonly IHomeSummaryCache _homeSummaryCache;
    private readonly IViewsSummaryCache _viewsSummaryCache;

    public CreatorCacheInvalidator(IHomeSummaryCache homeSummaryCache, IViewsSummaryCache viewsSummaryCache)
    {
        _homeSummaryCache = homeSummaryCache;
        _viewsSummaryCache = viewsSummaryCache;
    }

    public void Invalidate(int creatorId)
    {
        _homeSummaryCache.Remove(creatorId);
        _viewsSummaryCache.Remove(creatorId);
    }
}
