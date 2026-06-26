using CreatorPlatform.Analytics.Domain.PageViews;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IPageViewRepository
{
    Task AddAsync(PageView pageView, CancellationToken ct);
    Task<PageViewStatsRow> GetStatsAsync(int landingPageId, CancellationToken ct);
}

public sealed record PageViewStatsRow(
    long TotalViews,
    long UniqueVisitors,
    long ViewsToday,
    long UniqueVisitorsToday,
    long ViewsLast7Days,
    long UniqueVisitorsLast7Days,
    long ViewsLast30Days,
    long UniqueVisitorsLast30Days);
