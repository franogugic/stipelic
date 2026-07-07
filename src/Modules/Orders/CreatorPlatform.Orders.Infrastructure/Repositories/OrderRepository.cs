using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Orders.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly CreatorPlatformDbContext _context;

    public OrderRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _context.Set<Order>().AddAsync(order, ct);
    }

    public async Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct)
    {
        return await _context
            .Set<Order>()
            .FirstOrDefaultAsync(o => o.StripeCheckoutSessionId == stripeCheckoutSessionId, ct);
    }

    public async Task<Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct)
    {
        // Load without AsNoTracking so EF tracks the entity for update; PostgreSQL FOR UPDATE is handled at DB level via the transaction
        return await _context
            .Set<Order>()
            .FromSqlInterpolated($"""
                SELECT * FROM orders.orders
                WHERE "StripeCheckoutSessionId" = {stripeCheckoutSessionId}
                LIMIT 1
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct)
    {
        return await _context
            .Set<Order>()
            .FirstOrDefaultAsync(o => o.StripePaymentIntentId == stripePaymentIntentId, ct);
    }

    public async Task<List<OrderDto>> GetByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return await (
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            where c.Slug == creatorSlug && c.OwnerUserId == ownerUserId
            orderby o.CreatedAt descending
            select new OrderDto(
                o.PublicId,
                o.Email,
                o.Name,
                p.Name,
                o.AmountCents,
                o.Currency.ToString(),
                o.Status.ToString(),
                o.CreatedAt,
                o.PaidAt)
        ).ToListAsync(ct);
    }

    public async Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        var summary = await (
            from o in _context.Set<Order>().AsNoTracking()
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            where c.Slug == creatorSlug && c.OwnerUserId == ownerUserId
            group o by c.DefaultCurrency into g
            select new
            {
                Currency = g.Key,
                PaidOrderCount = g.Count(o => o.Status == OrderStatus.Paid),
                TotalPaidAmountCents = g.Sum(o => o.Status == OrderStatus.Paid ? o.AmountCents : 0)
            }
        ).FirstOrDefaultAsync(ct);

        return summary is null
            ? new OrderSummaryDto(0, 0, null)
            : new OrderSummaryDto(summary.PaidOrderCount, summary.TotalPaidAmountCents, summary.Currency.ToString());
    }

    public async Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
    {
        var summary = await (
            from o in _context.Set<Order>().AsNoTracking()
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            where o.LandingPageId == landingPageId
            group o by c.DefaultCurrency into g
            select new
            {
                Currency = g.Key,
                PaidOrderCount = g.Count(o => o.Status == OrderStatus.Paid),
                TotalPaidAmountCents = g.Sum(o => o.Status == OrderStatus.Paid ? o.AmountCents : 0)
            }
        ).FirstOrDefaultAsync(ct);

        return summary is null
            ? new OrderSummaryDto(0, 0, null)
            : new OrderSummaryDto(summary.PaidOrderCount, summary.TotalPaidAmountCents, summary.Currency.ToString());
    }

    public async Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(
        int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
    {
        // bucketUnit comes from a fixed server-side map (never from raw query string), so it is safe to
        // interpolate into date_trunc / generate_series. Zero-filled buckets via LEFT JOIN on generate_series.
        return await _context.Database.SqlQuery<PurchasesBucketRow>($"""
            WITH buckets AS (
                SELECT generate_series(
                    date_trunc({bucketUnit}, {cutoff}::timestamptz),
                    date_trunc({bucketUnit}, now()),
                    ('1 ' || {bucketUnit})::interval
                ) AS bucket_start
            )
            SELECT
                b.bucket_start                                      AS "BucketStart",
                COALESCE(COUNT(o."Id"), 0)::int                     AS "PurchaseCount",
                COALESCE(SUM(o."AmountCents"), 0)::int              AS "RevenueCents"
            FROM buckets b
            LEFT JOIN orders.orders o
                ON o."LandingPageId" = {landingPageId}
                AND o."Status" = 'Paid'
                AND date_trunc({bucketUnit}, o."CreatedAt") = b.bucket_start
            GROUP BY b.bucket_start
            ORDER BY b.bucket_start
            """)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<HomeSummaryDto> GetHomeSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        var creator = await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.Slug == creatorSlug && c.OwnerUserId == ownerUserId)
            .Select(c => new { c.Id, c.DefaultCurrency })
            .FirstOrDefaultAsync(ct);

        if (creator is null)
            return new HomeSummaryDto(0, 0, null, 0, 0, []);

        var orderStats = await _context.Set<Order>()
            .AsNoTracking()
            .Where(o => o.CreatorId == creator.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                PaidOrderCount = g.Count(o => o.Status == OrderStatus.Paid),
                TotalPaidAmountCents = g.Sum(o => o.Status == OrderStatus.Paid ? o.AmountCents : 0),
            })
            .FirstOrDefaultAsync(ct);

        var productCount = await _context.Set<Product>()
            .AsNoTracking()
            .CountAsync(p => p.CreatorId == creator.Id, ct);

        var landingPageCount = await _context.Set<LandingPage>()
            .AsNoTracking()
            .CountAsync(lp => lp.CreatorId == creator.Id, ct);

        var recentOrders = await (
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            where o.CreatorId == creator.Id && o.Status == OrderStatus.Paid
            orderby o.PaidAt descending
            select new OrderDto(
                o.PublicId,
                o.Email,
                o.Name,
                p.Name,
                o.AmountCents,
                o.Currency.ToString(),
                o.Status.ToString(),
                o.CreatedAt,
                o.PaidAt)
        ).Take(5).ToListAsync(ct);

        return new HomeSummaryDto(
            orderStats?.TotalPaidAmountCents ?? 0,
            orderStats?.PaidOrderCount ?? 0,
            creator.DefaultCurrency.ToString(),
            productCount,
            landingPageCount,
            recentOrders);
    }
}
