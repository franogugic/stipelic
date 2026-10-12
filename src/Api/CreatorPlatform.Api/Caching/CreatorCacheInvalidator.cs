using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Orders.Application.Interfaces;

namespace CreatorPlatform.Api.Caching;

/// <summary>The per-creator caches that hold plan-dependent, workspace-wide or timezone-bucketed numbers. The other
/// short-lived one (payout schedule: 60 s) expires on its own.</summary>
public sealed class CreatorCacheInvalidator : ICreatorCacheInvalidator
{
    private readonly IHomeSummaryCache _homeSummaryCache;
    private readonly IViewsSummaryCache _viewsSummaryCache;
    private readonly IDashboardTrendsCache _dashboardTrendsCache;

    public CreatorCacheInvalidator(
        IHomeSummaryCache homeSummaryCache, IViewsSummaryCache viewsSummaryCache, IDashboardTrendsCache dashboardTrendsCache)
    {
        _homeSummaryCache = homeSummaryCache;
        _viewsSummaryCache = viewsSummaryCache;
        _dashboardTrendsCache = dashboardTrendsCache;
    }

    public void Invalidate(int creatorId)
    {
        _homeSummaryCache.Remove(creatorId);
        _viewsSummaryCache.Remove(creatorId);
        _dashboardTrendsCache.Remove(creatorId);
    }
}
