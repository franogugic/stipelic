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

    public async Task AddAsync(PageView pageView, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO analytics.page_views ("Id", "LandingPageId", "VisitorId", "ViewedAt", "ViewedDate")
             VALUES ({pageView.Id}, {pageView.LandingPageId}, {pageView.VisitorId}, {pageView.ViewedAt}, {pageView.ViewedDate})
             ON CONFLICT ("LandingPageId", "VisitorId", "ViewedDate") DO NOTHING
             """,
            ct);
    }

    public async Task<PageViewStatsRow> GetStatsAsync(int landingPageId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfToday = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
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

    public async Task<List<ViewsBucketRow>> GetBucketedViewsAsync(
        int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
    {
        // bucketUnit comes from a fixed server-side map (never from raw query string), so it is safe to
        // interpolate into date_trunc / generate_series. Zero-filled buckets via LEFT JOIN on generate_series.
        return await _context.Database.SqlQuery<ViewsBucketRow>($"""
            WITH buckets AS (
                SELECT generate_series(
                    date_trunc({bucketUnit}, {cutoff}::timestamptz),
                    date_trunc({bucketUnit}, now()),
                    ('1 ' || {bucketUnit})::interval
                ) AS bucket_start
            )
            SELECT
                b.bucket_start                                      AS "BucketStart",
                COALESCE(COUNT(pv."Id"), 0)                         AS "ViewCount",
                COALESCE(COUNT(DISTINCT pv."VisitorId"), 0)         AS "UniqueVisitors"
            FROM buckets b
            LEFT JOIN analytics.page_views pv
                ON pv."LandingPageId" = {landingPageId}
                AND date_trunc({bucketUnit}, pv."ViewedAt") = b.bucket_start
            GROUP BY b.bucket_start
            ORDER BY b.bucket_start
            """)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
