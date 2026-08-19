using CreatorPlatform.Analytics.Domain.PageViews;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Domain.Contacts;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
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
        Guid? productPublicId,
        OrderStatus? status,
        DateTimeOffset? afterCreatedAt,
        Guid? afterId,
        int limit,
        CancellationToken ct)
    {
        // Left join on landing pages — Order.LandingPageId is nullable (direct-link/no-referrer
        // orders have none), so this must not drop rows the way an inner join would.
        // Filtered server-side (not client-side) so a status/product filter narrows the full order
        // set, not just whatever page happens to be loaded — keyset pagination below still walks the
        // filtered result set correctly since the cursor condition is applied on top of it. Product is
        // filtered by PublicId directly on the already-joined `p` — no extra round trip to resolve it
        // to an internal id first.
        var query =
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            join lp in _context.Set<LandingPage>().AsNoTracking() on o.LandingPageId equals (int?)lp.Id into lpJoin
            from lp in lpJoin.DefaultIfEmpty()
            where c.Slug == creatorSlug
                && c.OwnerUserId == ownerUserId
                && (!productPublicId.HasValue || p.PublicId == productPublicId.Value)
                && (!status.HasValue || o.Status == status.Value)
            select new { o, ProductName = p.Name, LandingPageTitle = (string?)lp.Title };

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
                x.o.AmountCents - x.o.PlatformFeeCents,
                x.LandingPageTitle))
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

    public async Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(
        string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        var rows = await (
            from o in _context.Set<Order>().AsNoTracking()
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            join lp in _context.Set<LandingPage>().AsNoTracking() on o.LandingPageId equals lp.Id
            where c.Slug == creatorSlug && c.OwnerUserId == ownerUserId && o.Status == OrderStatus.Paid
            group o by new { lp.PublicId, c.DefaultCurrency } into g
            select new
            {
                g.Key.PublicId,
                g.Key.DefaultCurrency,
                PurchaseCount = g.Count(),
                TotalRevenueCents = g.Sum(o => o.AmountCents)
            }
        ).ToListAsync(ct);

        return rows
            .Select(r => new LandingPageOrdersSummaryDto(r.PublicId, r.PurchaseCount, r.TotalRevenueCents, r.DefaultCurrency.ToString()))
            .ToList();
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
            return new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, ZeroTrend(), 0, 0, 0, 0, ZeroTrend(), ZeroMonthlyTrend(), 0);

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
                TotalPlatformFeeCents = g.Sum(o => o.Status == OrderStatus.Paid ? o.PlatformFeeCents : 0),
            })
            .FirstOrDefaultAsync(ct);

        // Four independent scalar counts (products, landing pages, all-time views, active subscribers) —
        // EF Core's DbContext isn't thread-safe for concurrent queries (Task.WhenAll on the same _context
        // throws at runtime), so true parallel execution isn't an option here. This collapses what were 4
        // separate round trips into 1 instead, each subquery backed by the same indexes the original LINQ
        // queries used (Product/LandingPage: (CreatorId, Status); ContactSummary/Unsubscribe: (CreatorId,
        // Email); CreatorViewTotal: PK). Status is stored as the enum's string name (HasConversion<string>),
        // matching the != 'Archived' literal below exactly.
        var counts = await _context.Database.SqlQuery<SummaryCountsRow>($"""
            SELECT
                (SELECT COUNT(*) FROM products.products
                    WHERE "CreatorId" = {creator.Id} AND "Status" != 'Archived')      AS "ProductCount",
                (SELECT COUNT(*) FROM landing_pages.landing_pages
                    WHERE "CreatorId" = {creator.Id} AND "Status" != 'Archived')      AS "LandingPageCount",
                COALESCE((SELECT "TotalViews" FROM analytics.creator_view_totals
                    WHERE "CreatorId" = {creator.Id}), 0)                             AS "TotalPageViews",
                (SELECT COUNT(*) FROM marketing.contact_summaries cs
                    WHERE cs."CreatorId" = {creator.Id}
                        AND NOT EXISTS (
                            SELECT 1 FROM marketing.unsubscribes u
                            WHERE u."CreatorId" = {creator.Id} AND u."Email" = cs."Email"
                        ))                                                            AS "SubscriberCount"
            """).AsNoTracking().FirstAsync(ct);

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
                o.AmountCents - o.PlatformFeeCents,
                null)
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

        // Daily revenue for the last TrendDays days. Bounded window (7 days of one creator's paid orders),
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

        // Daily page views for the same TrendDays window, bucketed by PageView.ViewedDate (already a UTC
        // calendar day, see PageView entity doc) — no DateTimeOffset→midnight conversion needed here.
        var trendStartDate = DateOnly.FromDateTime(trendStart.UtcDateTime);
        var viewDates = await (
            from pv in _context.Set<PageView>().AsNoTracking()
            join lp in _context.Set<LandingPage>().AsNoTracking() on pv.LandingPageId equals lp.Id
            where lp.CreatorId == creator.Id && pv.ViewedDate >= trendStartDate
            select pv.ViewedDate
        ).ToListAsync(ct);

        var todayDate = DateOnly.FromDateTime(todayMidnight.UtcDateTime);
        var viewsTrend = new int[TrendDays];
        foreach (var viewedDate in viewDates)
        {
            var diffDays = todayDate.DayNumber - viewedDate.DayNumber;
            var index = TrendDays - 1 - diffDays;
            if (index >= 0 && index < TrendDays)
                viewsTrend[index]++;
        }

        // Monthly revenue for the last MonthlyTrendMonths calendar months (oldest to newest, current
        // month included) — same in-memory bucketing approach as the daily revenueTrend above, since
        // month boundaries have variable lengths and aren't a clean SQL date-bucket without a calendar
        // table. Separate query from the 7-day trendRows above (different, much wider window).
        var monthlyTrendStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero)
            .AddMonths(-(MonthlyTrendMonths - 1));
        var monthlyRows = await _context.Set<Order>()
            .AsNoTracking()
            .Where(o => o.CreatorId == creator.Id && o.Status == OrderStatus.Paid && o.PaidAt >= monthlyTrendStart)
            .Select(o => new { o.PaidAt, o.AmountCents })
            .ToListAsync(ct);

        var monthlyRevenueTrend = new int[MonthlyTrendMonths];
        foreach (var row in monthlyRows)
        {
            if (row.PaidAt is not DateTimeOffset paidAt)
                continue;

            var paidUtc = paidAt.UtcDateTime;
            var monthDiff = (now.Year - paidUtc.Year) * 12 + (now.Month - paidUtc.Month);
            var index = MonthlyTrendMonths - 1 - monthDiff;
            if (index >= 0 && index < MonthlyTrendMonths)
                monthlyRevenueTrend[index] += row.AmountCents;
        }

        return new HomeSummaryDto(
            orderStats?.TotalPaidAmountCents ?? 0,
            orderStats?.PaidOrderCount ?? 0,
            creator.DefaultCurrency.ToString(),
            counts.ProductCount,
            counts.LandingPageCount,
            recentOrders,
            orderStats?.ThisMonthRevenueCents ?? 0,
            topProduct,
            [.. revenueTrend],
            emailsSentThisMonth,
            emailsMonthlyLimit,
            counts.TotalPageViews,
            counts.SubscriberCount,
            [.. viewsTrend],
            [.. monthlyRevenueTrend],
            orderStats?.TotalPlatformFeeCents ?? 0);
    }

    private const int TrendDays = 7;
    private const int MonthlyTrendMonths = 7;

    private static List<int> ZeroTrend() => [.. new int[TrendDays]];

    private static List<int> ZeroMonthlyTrend() => [.. new int[MonthlyTrendMonths]];

    private sealed record SummaryCountsRow(int ProductCount, int LandingPageCount, int TotalPageViews, int SubscriberCount);
}
