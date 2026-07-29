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

    public async Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug,
        int ownerUserId,
        DateTimeOffset? afterCreatedAt,
        Guid? afterId,
        int limit,
        CancellationToken ct)
    {
        var query =
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            where c.Slug == creatorSlug && c.OwnerUserId == ownerUserId
            select new { o, ProductName = p.Name };

        if (afterCreatedAt.HasValue && afterId.HasValue)
        {
            var cursorCreatedAt = afterCreatedAt.Value;
            var cursorId = afterId.Value;

            // Mirrors OrderKeysetCursor.IsBeforeCursor (see its tests) — inlined because EF Core
            // can't translate a call to an extracted predicate into SQL.
            query = query.Where(x =>
                x.o.CreatedAt < cursorCreatedAt ||
                (x.o.CreatedAt == cursorCreatedAt && x.o.PublicId.CompareTo(cursorId) < 0));
        }

        return await query
            .OrderByDescending(x => x.o.CreatedAt)
            .ThenByDescending(x => x.o.PublicId)
            .Take(limit)
            .Select(x => new OrderDto(
                x.o.PublicId,
                x.o.Email,
                x.o.Name,
                x.ProductName,
                x.o.AmountCents,
                x.o.Currency.ToString(),
                x.o.Status.ToString(),
                x.o.CreatedAt,
                x.o.PaidAt,
                x.o.PlatformFeeCents,
                x.o.AmountCents - x.o.PlatformFeeCents))
            .ToListAsync(ct);
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
            return new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, ZeroTrend(), 0, 0);

        var now = DateTimeOffset.UtcNow;
        var todayMidnight = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var trendStart = todayMidnight.AddDays(-(TrendDays - 1));
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var orderStats = await _context.Set<Order>()
            .AsNoTracking()
            .Where(o => o.CreatorId == creator.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                PaidOrderCount = g.Count(o => o.Status == OrderStatus.Paid),
                TotalPaidAmountCents = g.Sum(o => o.Status == OrderStatus.Paid ? o.AmountCents : 0),
                ThisMonthRevenueCents = g.Sum(o => o.Status == OrderStatus.Paid && o.PaidAt >= monthStart ? o.AmountCents : 0),
            })
            .FirstOrDefaultAsync(ct);

        var productCount = await _context.Set<Product>()
            .AsNoTracking()
            .CountAsync(p => p.CreatorId == creator.Id && p.Status != ProductStatus.Archived, ct);

        var landingPageCount = await _context.Set<LandingPage>()
            .AsNoTracking()
            .CountAsync(lp => lp.CreatorId == creator.Id && lp.Status != LandingPageStatus.Archived, ct);

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
                o.PaidAt,
                o.PlatformFeeCents,
                o.AmountCents - o.PlatformFeeCents)
        ).Take(5).ToListAsync(ct);

        // Top product by paid revenue (all-time).
        var topRow = await (
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            where o.CreatorId == creator.Id && o.Status == OrderStatus.Paid
            group new { o, p } by new { o.ProductId, p.Name } into g
            select new { g.Key.Name, Total = g.Sum(x => x.o.AmountCents) }
        ).OrderByDescending(x => x.Total).FirstOrDefaultAsync(ct);

        var topProduct = topRow is null ? null : new TopProductDto(topRow.Name, topRow.Total);

        const string emailSendsLimitKey = "max_email_sends_per_month";

        var emailsMonthlyLimit = await (
            from cs in _context.Set<CreatorSubscription>().AsNoTracking()
            join limit in _context.Set<CreatorPlanLimit>().AsNoTracking()
                on cs.PlanId equals limit.PlanId
            where cs.CreatorId == creator.Id
                && cs.Status == CreatorSubscriptionStatus.Active
                && limit.LimitKey == emailSendsLimitKey
            select (int?)limit.LimitValue
        ).FirstOrDefaultAsync(ct) ?? 0;

        // Same calendar-month boundary as UsagePeriodResolver.Resolve(CalendarMonth, now) — matches
        // monthStart exactly, so this reads the same counter row ICreatorUsageService writes to.
        var emailsSentThisMonth = await _context.Set<CreatorUsageCounter>()
            .AsNoTracking()
            .Where(c => c.CreatorId == creator.Id && c.UsageKey == emailSendsLimitKey && c.PeriodStart == monthStart)
            .Select(c => (int?)c.UsedValue)
            .FirstOrDefaultAsync(ct) ?? 0;

        // Daily revenue for the last TrendDays days. Bounded window (14 days of one creator's paid orders),
        // so we pull the rows and bucket in memory rather than doing SQL date bucketing.
        var trendRows = await _context.Set<Order>()
            .AsNoTracking()
            .Where(o => o.CreatorId == creator.Id && o.Status == OrderStatus.Paid && o.PaidAt >= trendStart)
            .Select(o => new { o.PaidAt, o.AmountCents })
            .ToListAsync(ct);

        var revenueTrend = new int[TrendDays];
        foreach (var row in trendRows)
        {
            if (row.PaidAt is not DateTimeOffset paidAt)
                continue;

            var paidUtc = paidAt.UtcDateTime;
            var paidMidnight = new DateTimeOffset(paidUtc.Year, paidUtc.Month, paidUtc.Day, 0, 0, 0, TimeSpan.Zero);
            var diffDays = (int)Math.Round((todayMidnight - paidMidnight).TotalDays);
            var index = TrendDays - 1 - diffDays;
            if (index >= 0 && index < TrendDays)
                revenueTrend[index] += row.AmountCents;
        }

        return new HomeSummaryDto(
            orderStats?.TotalPaidAmountCents ?? 0,
            orderStats?.PaidOrderCount ?? 0,
            creator.DefaultCurrency.ToString(),
            productCount,
            landingPageCount,
            recentOrders,
            orderStats?.ThisMonthRevenueCents ?? 0,
            topProduct,
            [.. revenueTrend],
            emailsSentThisMonth,
            emailsMonthlyLimit);
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

    private const int TrendDays = 14;

    private static List<int> ZeroTrend() => [.. new int[TrendDays]];
}
