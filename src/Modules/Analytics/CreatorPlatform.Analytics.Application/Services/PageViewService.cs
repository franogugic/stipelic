using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.PageViews;

namespace CreatorPlatform.Analytics.Application.Services;

public sealed class PageViewService : IPageViewService
{
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromHours(24);

    private readonly IPageViewRepository _repository;

    public PageViewService(IPageViewRepository repository)
    {
        _repository = repository;
    }

    public async Task RecordAsync(int landingPageId, Guid visitorId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var since = now - DeduplicationWindow;

        var hasRecentView = await _repository.HasRecentViewAsync(landingPageId, visitorId, since, ct);
        if (hasRecentView)
            return;

        var pageView = new PageView
        {
            Id = Guid.NewGuid(),
            LandingPageId = landingPageId,
            VisitorId = visitorId,
            ViewedAt = now
        };

        await _repository.AddAsync(pageView, ct);
    }

    public async Task<LandingPageAnalyticsResponseDto> GetLandingPageStatsAsync(int landingPageId, CancellationToken ct)
    {
        var stats = await _repository.GetStatsAsync(landingPageId, ct);

        return new LandingPageAnalyticsResponseDto
        {
            AllTime = new PeriodStatsDto
            {
                TotalViews = stats.TotalViews,
                UniqueVisitors = stats.UniqueVisitors
            },
            Today = new PeriodStatsDto
            {
                TotalViews = stats.ViewsToday,
                UniqueVisitors = stats.UniqueVisitorsToday
            },
            Last7Days = new PeriodStatsDto
            {
                TotalViews = stats.ViewsLast7Days,
                UniqueVisitors = stats.UniqueVisitorsLast7Days
            },
            Last30Days = new PeriodStatsDto
            {
                TotalViews = stats.ViewsLast30Days,
                UniqueVisitors = stats.UniqueVisitorsLast30Days
            }
        };
    }
}
