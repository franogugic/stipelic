using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Services;

public sealed class OrderContextProvider : IOrderContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public OrderContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<int, ProductRevenueDto>> GetProductRevenueByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        var rows = await _context.Set<Order>()
            .AsNoTracking()
            .Where(o => o.CreatorId == creatorId)
            .GroupBy(o => o.ProductId)
            .Select(g => new ProductRevenueDto(
                g.Key,
                g.Sum(o => o.Status == OrderStatus.Paid ? o.AmountCents : 0),
                g.Count(o => o.Status == OrderStatus.Paid)))
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.ProductId);
    }

    public async Task<Dictionary<int, int[]>> GetProductSalesTrendsAsync(int creatorId, DateTimeOffset firstMonth, CancellationToken ct)
    {
        // generate_series yields the six UTC month starts; each product's paid orders are counted per month with
        // FILTER, in one pass over the creator's orders in the window.
        var lastMonth = firstMonth.AddMonths(ProductSalesTrend.Months - 1);
        var windowEnd = lastMonth.AddMonths(1);

        var rows = await _context.Database.SqlQuery<ProductMonthRow>($"""
            WITH paid AS (
                SELECT o."ProductId" AS "ProductId", COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE 'UTC' AS "At"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId} AND o."Status" = 'Paid'
                  AND COALESCE(o."PaidAt", o."CreatedAt") >= {firstMonth}
                  AND COALESCE(o."PaidAt", o."CreatedAt") < {windowEnd}
            )
            SELECT p."ProductId" AS "ProductId",
                   (EXTRACT(YEAR FROM m."MonthStart")::int * 12 + EXTRACT(MONTH FROM m."MonthStart")::int) AS "MonthKey",
                   COUNT(*) FILTER (WHERE date_trunc('month', p."At") = m."MonthStart")::int AS "Sales"
            FROM generate_series(
                {firstMonth}::timestamptz AT TIME ZONE 'UTC',
                {lastMonth}::timestamptz AT TIME ZONE 'UTC',
                '1 month'::interval) AS m("MonthStart")
            CROSS JOIN paid p
            GROUP BY p."ProductId", m."MonthStart"
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        var firstKey = firstMonth.Year * 12 + firstMonth.Month;
        return rows
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g =>
            {
                var trend = new int[ProductSalesTrend.Months];
                foreach (var row in g)
                    trend[row.MonthKey - firstKey] = row.Sales;
                return trend;
            });
    }

    private sealed record ProductMonthRow(int ProductId, int MonthKey, int Sales);

    // Totals and buckets come back from one statement (a 'total' row plus one 'bucket' row per day / month), so the
    // product's orders are read once. `unit` comes from a fixed server-side map ("day" / "month") and is still sent as
    // a parameter; bucketing happens on UTC `timestamp` values, so the session TimeZone doesn't matter.
    public async Task<ProductOrderStatsDto> GetProductOrderStatsAsync(
        int creatorId, int productId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket,
        DateTimeOffset monthStart, CancellationToken ct)
    {
        var step = "1 " + unit;
        var windowEnd = unit == "month" ? lastBucket.AddMonths(1) : lastBucket.AddDays(1);

        var rows = await _context.Database.SqlQuery<ProductOrderStatsRow>($"""
            WITH paid AS (
                SELECT o."AmountCents" AS "AmountCents", COALESCE(o."PaidAt", o."CreatedAt") AS "At"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId} AND o."ProductId" = {productId} AND o."Status" = 'Paid'
            )
            SELECT
                'total' AS "Kind",
                '' AS "BucketStart",
                COALESCE(SUM(p."AmountCents"), 0)::bigint AS "RevenueCents",
                COUNT(*)::int AS "SalesCount",
                COALESCE(SUM(p."AmountCents") FILTER (WHERE p."At" >= {monthStart}), 0)::bigint AS "ThisMonthCents"
            FROM paid p
            UNION ALL
            SELECT
                'bucket',
                to_char(b."BucketStart", 'YYYY-MM-DD'),
                COALESCE(r."Total", 0)::bigint,
                0,
                0::bigint
            FROM generate_series(
                {firstBucket}::timestamptz AT TIME ZONE 'UTC',
                {lastBucket}::timestamptz AT TIME ZONE 'UTC',
                {step}::interval) AS b("BucketStart")
            LEFT JOIN (
                SELECT date_trunc({unit}, p."At" AT TIME ZONE 'UTC') AS "Bucket", SUM(p."AmountCents") AS "Total"
                FROM paid p
                WHERE p."At" >= {firstBucket} AND p."At" < {windowEnd}
                GROUP BY 1
            ) r ON r."Bucket" = b."BucketStart"
            ORDER BY 1, 2
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        var total = rows.Single(r => r.Kind == "total");
        return new ProductOrderStatsDto(
            total.RevenueCents,
            total.SalesCount,
            total.ThisMonthCents,
            rows.Where(r => r.Kind == "bucket").Select(r => new ProductRevenuePointDto(r.BucketStart, r.RevenueCents)).ToList());
    }

    private sealed record ProductOrderStatsRow(string Kind, string BucketStart, long RevenueCents, int SalesCount, long ThisMonthCents);
}
