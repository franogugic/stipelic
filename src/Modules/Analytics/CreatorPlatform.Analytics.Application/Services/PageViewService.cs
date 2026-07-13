using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.PageViews;

namespace CreatorPlatform.Analytics.Application.Services;

public sealed class PageViewService : IPageViewService
{
    private readonly IPageViewRepository _repository;
    private readonly IViewsSummaryCache _viewsSummaryCache;

    public PageViewService(IPageViewRepository repository, IViewsSummaryCache viewsSummaryCache)
    {
        _repository = repository;
        _viewsSummaryCache = viewsSummaryCache;
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
            TotalEmailCaptures = 0,
            PurchaseCount = 0,
            TotalRevenueCents = 0,
            Currency = null
        };
    }

    public async Task<List<LandingPageViewsSummaryDto>> GetViewsSummaryByCreatorAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        if (_viewsSummaryCache.TryGet(creatorSlug, out var cached) && cached is not null)
            return cached;

        var result = await _repository.GetViewsSummaryByCreatorAsync(creatorSlug, ownerUserId, ct);

        _viewsSummaryCache.Set(creatorSlug, result);

        return result;
    }
}
