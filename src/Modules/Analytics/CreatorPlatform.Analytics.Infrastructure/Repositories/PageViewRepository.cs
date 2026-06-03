using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.PageViews;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Analytics.Infrastructure.Repositories;

public sealed class PageViewRepository : IPageViewRepository
{
    private readonly CreatorPlatformDbContext _context;

    public PageViewRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasRecentViewAsync(int landingPageId, Guid visitorId, DateTimeOffset since, CancellationToken ct)
    {
        return await _context
            .Set<PageView>()
            .AsNoTracking()
            .AnyAsync(pv =>
                pv.LandingPageId == landingPageId &&
                pv.VisitorId == visitorId &&
                pv.ViewedAt >= since,
                ct);
    }

    public async Task AddAsync(PageView pageView, CancellationToken ct)
    {
        await _context.Set<PageView>().AddAsync(pageView, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<PageViewStatsRow> GetStatsAsync(int landingPageId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfToday = now.Date;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        var result = await _context.Database
            .SqlQuery<PageViewStatsRow>($"""
                SELECT
                    COUNT(*)                                                                         AS "TotalViews",
                    COUNT(DISTINCT "VisitorId")                                                      AS "UniqueVisitors",
                    COUNT(*) FILTER (WHERE "ViewedAt" >= {startOfToday})                             AS "ViewsToday",
                    COUNT(DISTINCT "VisitorId") FILTER (WHERE "ViewedAt" >= {startOfToday})          AS "UniqueVisitorsToday",
                    COUNT(*) FILTER (WHERE "ViewedAt" >= {sevenDaysAgo})                             AS "ViewsLast7Days",
                    COUNT(DISTINCT "VisitorId") FILTER (WHERE "ViewedAt" >= {sevenDaysAgo})          AS "UniqueVisitorsLast7Days",
                    COUNT(*) FILTER (WHERE "ViewedAt" >= {thirtyDaysAgo})                            AS "ViewsLast30Days",
                    COUNT(DISTINCT "VisitorId") FILTER (WHERE "ViewedAt" >= {thirtyDaysAgo})         AS "UniqueVisitorsLast30Days"
                FROM analytics.page_views
                WHERE "LandingPageId" = {landingPageId}
                """)
            .AsNoTracking()
            .FirstAsync(ct);

        return result;
    }
}
