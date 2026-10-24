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
                g.Sum(o => o.Status == OrderStatus.Paid ? o.KeptAmountCents : 0),
                g.Count(o => o.Status == OrderStatus.Paid),
                g.Count()))
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
}
