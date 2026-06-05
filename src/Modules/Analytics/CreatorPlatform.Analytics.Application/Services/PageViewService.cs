using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.PageViews;

namespace CreatorPlatform.Analytics.Application.Services;

public sealed class PageViewService : IPageViewService
{
    private readonly IPageViewRepository _repository;

    public PageViewService(IPageViewRepository repository)
    {
        _repository = repository;
    }

    public async Task RecordAsync(int landingPageId, Guid visitorId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var pageView = new PageView
        {
            Id = Guid.NewGuid(),
            LandingPageId = landingPageId,
            VisitorId = visitorId,
            ViewedAt = now,
            ViewedDate = today
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
            },
            TotalEmailCaptures = 0
        };
    }
}
