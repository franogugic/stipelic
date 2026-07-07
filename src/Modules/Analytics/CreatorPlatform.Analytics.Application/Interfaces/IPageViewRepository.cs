using CreatorPlatform.Analytics.Domain.PageViews;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IPageViewRepository
{
    Task AddAsync(PageView pageView, CancellationToken ct);
    Task<PageViewStatsRow> GetStatsAsync(int landingPageId, CancellationToken ct);
    Task<List<ViewsBucketRow>> GetBucketedViewsAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct);
}

public sealed record ViewsBucketRow(
    DateTimeOffset BucketStart,
    long ViewCount,
    long UniqueVisitors);

public sealed record PageViewStatsRow(
    long TotalViews,
    long UniqueVisitors,
    long ViewsToday,
    long UniqueVisitorsToday,
    long ViewsLast7Days,
    long UniqueVisitorsLast7Days,
    long ViewsLast30Days,
    long UniqueVisitorsLast30Days);
