using CreatorPlatform.Analytics.Domain.PageViews;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Domain.Contacts;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Application.Analytics;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Application.Time;
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
        Guid? landingPagePublicId,
        OrderStatus? status,
        string? customerSearch,
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
                && (!landingPagePublicId.HasValue || lp.PublicId == landingPagePublicId.Value)
                && (!status.HasValue || o.Status == status.Value)
            select new { o, ProductName = p.Name, LandingPageTitle = (string?)lp.Title };

        if (customerSearch is not null)
        {
            // Case-insensitive substring match on email OR name. Written as lower(column) LIKE '%term%' so it
            // matches the GIN trigram indexes on lower("Email") / lower("Name") exactly (see migration
            // AddOrderCustomerSearchIndexes). The term's own %, _ and \ are escaped so they match literally.
            var pattern = LikePatterns.Contains(customerSearch.ToLowerInvariant());
            query = query.Where(x =>
                (x.o.Email != null && EF.Functions.Like(x.o.Email.ToLower(), pattern, LikePatterns.EscapeCharacter))
                || (x.o.Name != null && EF.Functions.Like(x.o.Name.ToLower(), pattern, LikePatterns.EscapeCharacter)));
        }

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

    public async Task<int?> GetCreatorIdForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.Slug == creatorSlug
                && c.OwnerUserId == ownerUserId
                && c.Status != CreatorStatus.Disabled)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
    }

    // Both trend queries bucket on local `timestamp` values in the creator's timezone (AT TIME ZONE tz), never on
    // timestamptz, so the result doesn't depend on the session TimeZone; firstBucket / lastBucket are the instants the
    // first and last local buckets start. `unit` comes from a fixed
    // server-side map ("day" / "month") and is still passed as a parameter. generate_series supplies every bucket;
    // the LEFT JOIN leaves empty ones at 0.
    public async Task<List<TrendBucketRow>> GetRevenueTrendAsync(
        int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, string timezone, CancellationToken ct)
    {
        var step = "1 " + unit;
        var windowEnd = NextBucketStart(unit, lastBucket, timezone);

        // Revenue is booked when it was paid; orders from before PaidAt was recorded fall back to CreatedAt.
        return await _context.Database.SqlQuery<TrendBucketRow>($"""
            SELECT
                to_char(b."BucketStart", 'YYYY-MM-DD') AS "BucketStart",
                COALESCE(r."Total", 0)::bigint AS "Value"
            FROM generate_series(
                {firstBucket}::timestamptz AT TIME ZONE {timezone},
                {lastBucket}::timestamptz AT TIME ZONE {timezone},
                {step}::interval) AS b("BucketStart")
            LEFT JOIN (
                SELECT
                    date_trunc({unit}, COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE {timezone}) AS "Bucket",
                    SUM(o."AmountCents") AS "Total"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId}
                  AND o."Status" = 'Paid'
                  AND COALESCE(o."PaidAt", o."CreatedAt") >= {firstBucket}
                  AND COALESCE(o."PaidAt", o."CreatedAt") < {windowEnd}
                GROUP BY 1
            ) r ON r."Bucket" = b."BucketStart"
            ORDER BY b."BucketStart"
            """)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<List<TrendBucketRow>> GetViewsTrendAsync(
        int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, string timezone, CancellationToken ct)
    {
        var step = "1 " + unit;
        var windowEnd = NextBucketStart(unit, lastBucket, timezone);

        // (LandingPageId, ViewedAt) index per page of the creator; archived pages count too (their views happened).
        return await _context.Database.SqlQuery<TrendBucketRow>($"""
            SELECT
                to_char(b."BucketStart", 'YYYY-MM-DD') AS "BucketStart",
                COALESCE(v."Total", 0)::bigint AS "Value"
            FROM generate_series(
                {firstBucket}::timestamptz AT TIME ZONE {timezone},
                {lastBucket}::timestamptz AT TIME ZONE {timezone},
                {step}::interval) AS b("BucketStart")
            LEFT JOIN (
                SELECT
                    date_trunc({unit}, pv."ViewedAt" AT TIME ZONE {timezone}) AS "Bucket",
                    COUNT(*) AS "Total"
                FROM analytics.page_views pv
                JOIN landing_pages.landing_pages lp ON lp."Id" = pv."LandingPageId"
                WHERE lp."CreatorId" = {creatorId}
                  AND pv."ViewedAt" >= {firstBucket}
                  AND pv."ViewedAt" < {windowEnd}
                GROUP BY 1
            ) v ON v."Bucket" = b."BucketStart"
            ORDER BY b."BucketStart"
            """)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private static DateTimeOffset NextBucketStart(string unit, DateTimeOffset lastBucket, string timezone)
    {
        var local = CreatorTimeZone.LocalNow(lastBucket, timezone);
        return CreatorTimeZone.ToInstant(unit == "month" ? local.AddMonths(1) : local.AddDays(1), timezone);
    }

    public async Task<string> GetCreatorTimezoneAsync(int creatorId, CancellationToken ct) =>
        CreatorTimeZone.Resolve((await _context.Database.SqlQuery<string?>($"""
            SELECT s."Timezone" AS "Value" FROM creators.creator_settings s WHERE s."CreatorId" = {creatorId}
            """).ToListAsync(ct)).SingleOrDefault());

    public async Task<bool> CreatorExistsForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return await _context.Set<Creator>()
            .AsNoTracking()
            .AnyAsync(c => c.Slug == creatorSlug
                && c.OwnerUserId == ownerUserId
                && c.Status != CreatorStatus.Disabled, ct);
    }

    public async Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        // Disabled workspaces are excluded: their slug can be reused by a new workspace. An unknown slug (or another
        // owner's) answers zeros, as before.
        var creator = (await _context.Database.SqlQuery<SummaryCreatorRow>($"""
            SELECT c."Id" AS "Id", s."Timezone" AS "Timezone"
            FROM creators.creators c
            LEFT JOIN creators.creator_settings s ON s."CreatorId" = c."Id"
            WHERE c."Slug" = {creatorSlug} AND c."OwnerUserId" = {ownerUserId} AND c."Status" <> 'Disabled'
            """).AsNoTracking().ToListAsync(ct)).SingleOrDefault();
        if (creator is null)
            return ToSummaryDto(new OrderSummaryRow(0, 0, 0, 0, 0, 0, 0, 0, null), []);

        // One pass over the creator's orders via the (CreatorId, CreatedAt) index; no GROUP BY, so always one row.
        var totals = (await _context.Database.SqlQuery<OrderSummaryRow>($"""
            SELECT
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid'))::int AS "PaidOrderCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Paid'), 0)::int AS "TotalPaidAmountCents",
                COALESCE(SUM(o."PlatformFeeCents") FILTER (WHERE o."Status" = 'Paid'), 0)::int AS "TotalPlatformFeeCents",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Refunded'))::int AS "RefundedOrderCount",
                COUNT(o."Id")::int AS "TotalOrderCount",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Pending'))::int AS "PendingOrderCount",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Failed'))::int AS "FailedOrderCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Refunded'), 0)::int AS "RefundedAmountCents",
                MAX(c."DefaultCurrency") FILTER (WHERE o."Id" IS NOT NULL) AS "Currency"
            FROM creators.creators c
            LEFT JOIN orders.orders o ON o."CreatorId" = c."Id"
            WHERE c."Id" = {creator.Id}
            """).AsNoTracking().ToListAsync(ct)).Single();

        return ToSummaryDto(totals, await GetMonthlyTrendAsync(creator.Id, CreatorTimeZone.Resolve(creator.Timezone), ct));
    }

    /// <summary>Gross / fees / net of Paid orders by the month they were paid, and refunds by the month they were
    /// refunded — 12 local months in <paramref name="timezone"/>, zero-filled by generate_series.</summary>
    private async Task<List<OrderMonthDto>> GetMonthlyTrendAsync(int creatorId, string timezone, CancellationToken ct)
    {
        var rows = await _context.Database.SqlQuery<OrderMonthRow>($"""
            WITH months AS (
                SELECT m AS "MonthStart"
                FROM generate_series(
                    date_trunc('month', now() AT TIME ZONE {timezone}) - interval '11 months',
                    date_trunc('month', now() AT TIME ZONE {timezone}),
                    interval '1 month') AS m
            ),
            local_orders AS (
                SELECT o."Status", o."AmountCents", o."PlatformFeeCents",
                       date_trunc('month', COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE {timezone}) AS "PaidMonth",
                       date_trunc('month', o."UpdatedAt" AT TIME ZONE {timezone}) AS "UpdatedMonth"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId} AND o."Status" IN ('Paid', 'Refunded')
            )
            SELECT to_char(m."MonthStart", 'YYYY-MM') AS "Month",
                   COALESCE(SUM(lo."AmountCents") FILTER (WHERE lo."Status" = 'Paid' AND lo."PaidMonth" = m."MonthStart"), 0)::bigint AS "GrossCents",
                   COALESCE(SUM(lo."PlatformFeeCents") FILTER (WHERE lo."Status" = 'Paid' AND lo."PaidMonth" = m."MonthStart"), 0)::bigint AS "FeeCents",
                   (COUNT(*) FILTER (WHERE lo."Status" = 'Refunded' AND lo."UpdatedMonth" = m."MonthStart"))::int AS "RefundedCount"
            FROM months m
            LEFT JOIN local_orders lo ON lo."PaidMonth" = m."MonthStart" OR lo."UpdatedMonth" = m."MonthStart"
            GROUP BY m."MonthStart"
            ORDER BY m."MonthStart"
            """).AsNoTracking().ToListAsync(ct);

        return rows.Select(r => new OrderMonthDto(r.Month, r.GrossCents, r.FeeCents, r.GrossCents - r.FeeCents, r.RefundedCount)).ToList();
    }

    private sealed record SummaryCreatorRow(int Id, string? Timezone);

    private sealed record OrderMonthRow(string Month, long GrossCents, long FeeCents, int RefundedCount);

    public async Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
    {
        var rows = await _context.Database.SqlQuery<OrderSummaryRow>($"""
            SELECT
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid'))::int AS "PaidOrderCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Paid'), 0)::int AS "TotalPaidAmountCents",
                COALESCE(SUM(o."PlatformFeeCents") FILTER (WHERE o."Status" = 'Paid'), 0)::int AS "TotalPlatformFeeCents",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Refunded'))::int AS "RefundedOrderCount",
                COUNT(o."Id")::int AS "TotalOrderCount",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Pending'))::int AS "PendingOrderCount",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Failed'))::int AS "FailedOrderCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Refunded'), 0)::int AS "RefundedAmountCents",
                MAX(c."DefaultCurrency") AS "Currency"
            FROM orders.orders o
            JOIN creators.creators c ON c."Id" = o."CreatorId"
            WHERE o."LandingPageId" = {landingPageId}
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        return ToSummaryDto(rows.Single(), []);
    }

    public async Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(
        int landingPageId, StatsPeriods periods, CancellationToken ct)
    {
        var row = (await _context.Database.SqlQuery<SalesByPeriodRow>($"""
            SELECT
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.StartOfToday}))::int AS "TodayCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.StartOfToday}), 0)::bigint AS "TodayRevenueCents",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.Last7DaysFrom}))::int AS "Last7DaysCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.Last7DaysFrom}), 0)::bigint AS "Last7DaysRevenueCents",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.Last30DaysFrom}))::int AS "Last30DaysCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.Last30DaysFrom}), 0)::bigint AS "Last30DaysRevenueCents",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid'))::int AS "AllTimeCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Paid'), 0)::bigint AS "AllTimeRevenueCents",
                MAX(c."DefaultCurrency") AS "Currency"
            FROM orders.orders o
            JOIN creators.creators c ON c."Id" = o."CreatorId"
            WHERE o."LandingPageId" = {landingPageId}
            """)
            .AsNoTracking()
            .ToListAsync(ct)).Single();

        return new LandingPageSalesByPeriodDto(
            new PeriodSalesDto(row.TodayCount, row.TodayRevenueCents),
            new PeriodSalesDto(row.Last7DaysCount, row.Last7DaysRevenueCents),
            new PeriodSalesDto(row.Last30DaysCount, row.Last30DaysRevenueCents),
            new PeriodSalesDto(row.AllTimeCount, row.AllTimeRevenueCents),
            row.Currency);
    }

    private sealed record SalesByPeriodRow(
        int TodayCount,
        long TodayRevenueCents,
        int Last7DaysCount,
        long Last7DaysRevenueCents,
        int Last30DaysCount,
        long Last30DaysRevenueCents,
        int AllTimeCount,
        long AllTimeRevenueCents,
        string? Currency);

    private sealed record OrderSummaryRow(
        int PaidOrderCount,
        int TotalPaidAmountCents,
        int TotalPlatformFeeCents,
        int RefundedOrderCount,
        int TotalOrderCount,
        int PendingOrderCount,
        int FailedOrderCount,
        int RefundedAmountCents,
        string? Currency);

    private static OrderSummaryDto ToSummaryDto(OrderSummaryRow row, List<OrderMonthDto> monthlyTrend) => new(
        row.PaidOrderCount,
        row.TotalPaidAmountCents,
        row.Currency,
        row.TotalPlatformFeeCents,
        row.TotalPaidAmountCents - row.TotalPlatformFeeCents,
        row.RefundedOrderCount,
        row.TotalOrderCount,
        row.PendingOrderCount,
        row.FailedOrderCount,
        row.RefundedAmountCents,
        monthlyTrend);

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

    public async Task<HomeSummaryDto> GetHomeSummaryByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        var creator = await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.Id == creatorId)
            .Select(c => new { c.Id, c.DefaultCurrency })
            .FirstOrDefaultAsync(ct);

        if (creator is null)
            return new HomeSummaryDto(
                0, 0, null, 0, 0, [], 0, null, ZeroTrend(), 0, 0, 0, 0, ZeroTrend(), ZeroMonthlyTrend(), 0,
                [.. new int[BoardMonths]], 0, 0, 0);

        var now = DateTimeOffset.UtcNow;
        // Analytics use the creator's own days and months; only the email-sends usage counter below stays on the UTC
        // calendar month (it is a billing counter).
        var timezone = await GetCreatorTimezoneAsync(creator.Id, ct);
        var local = CreatorTimeZone.LocalNow(now, timezone);
        var firstTrendDay = local.Date.AddDays(-(TrendDays - 1));
        var trendStart = CreatorTimeZone.ToInstant(firstTrendDay, timezone);
        var thisLocalMonth = new DateTime(local.Year, local.Month, 1);
        var monthStart = CreatorTimeZone.ToInstant(thisLocalMonth, timezone);
        var usageMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

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

        // Same calendar-month boundary as UsagePeriodResolver.Resolve(CalendarMonth, now) (UTC) — so this reads the
        // same counter row ICreatorUsageService writes to.
        var emailsSentThisMonth = await _context.Set<CreatorUsageCounter>()
            .AsNoTracking()
            .Where(c => c.CreatorId == creator.Id && c.UsageKey == emailSendsLimitKey && c.PeriodStart == usageMonthStart)
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

            var index = (CreatorTimeZone.LocalNow(paidAt, timezone).Date - firstTrendDay).Days;
            if (index >= 0 && index < TrendDays)
                revenueTrend[index] += row.AmountCents;
        }

        // Daily page views for the same TrendDays window, by the creator's local day of ViewedAt.
        var viewTimes = await (
            from pv in _context.Set<PageView>().AsNoTracking()
            join lp in _context.Set<LandingPage>().AsNoTracking() on pv.LandingPageId equals lp.Id
            where lp.CreatorId == creator.Id && pv.ViewedAt >= trendStart
            select pv.ViewedAt
        ).ToListAsync(ct);

        var viewsTrend = new int[TrendDays];
        foreach (var viewedAt in viewTimes)
        {
            var index = (CreatorTimeZone.LocalNow(viewedAt, timezone).Date - firstTrendDay).Days;
            if (index >= 0 && index < TrendDays)
                viewsTrend[index]++;
        }

        // Monthly revenue for the last MonthlyTrendMonths calendar months (oldest to newest, current
        // month included) — same in-memory bucketing approach as the daily revenueTrend above, since
        // month boundaries have variable lengths and aren't a clean SQL date-bucket without a calendar
        // table. Separate query from the 7-day trendRows above (different, much wider window).
        var firstTrendMonth = thisLocalMonth.AddMonths(-(MonthlyTrendMonths - 1));
        var monthlyTrendStart = CreatorTimeZone.ToInstant(firstTrendMonth, timezone);
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

            var paidLocal = CreatorTimeZone.LocalNow(paidAt, timezone);
            var index = (paidLocal.Year - firstTrendMonth.Year) * 12 + (paidLocal.Month - firstTrendMonth.Month);
            if (index >= 0 && index < MonthlyTrendMonths)
                monthlyRevenueTrend[index] += row.AmountCents;
        }

        var board = await GetBoardFiguresAsync(creator.Id, timezone, ct);

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
            orderStats?.TotalPlatformFeeCents ?? 0,
            board.MonthlyPaidOrderCounts,
            board.CustomerCount,
            board.RefundedOrderCount,
            board.RefundedAmountCents);
    }

    private const int BoardMonths = 12;

    private sealed record BoardFigures(List<int> MonthlyPaidOrderCounts, int CustomerCount, int RefundedOrderCount, int RefundedAmountCents);

    /// <summary>The dashboard's "sales at a glance" board: customers and refunds over all time, and paid orders per
    /// month in the creator's timezone (for the average order this month vs last month).</summary>
    private async Task<BoardFigures> GetBoardFiguresAsync(int creatorId, string timezone, CancellationToken ct)
    {
        var totals = (await _context.Database.SqlQuery<BoardTotalsRow>($"""
            SELECT
                (COUNT(DISTINCT lower(o."Email")) FILTER (WHERE o."Status" = 'Paid'))::int AS "CustomerCount",
                (COUNT(*) FILTER (WHERE o."Status" = 'Refunded'))::int AS "RefundedOrderCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Refunded'), 0)::int AS "RefundedAmountCents"
            FROM orders.orders o
            WHERE o."CreatorId" = {creatorId}
            """).AsNoTracking().ToListAsync(ct)).Single();

        var months = await _context.Database.SqlQuery<int>($"""
            SELECT COALESCE(p."Count", 0)::int AS "Value"
            FROM generate_series(
                date_trunc('month', now() AT TIME ZONE {timezone}) - interval '11 months',
                date_trunc('month', now() AT TIME ZONE {timezone}),
                interval '1 month') AS m("MonthStart")
            LEFT JOIN (
                SELECT date_trunc('month', COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE {timezone}) AS "Month", COUNT(*) AS "Count"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId} AND o."Status" = 'Paid'
                GROUP BY 1
            ) p ON p."Month" = m."MonthStart"
            ORDER BY m."MonthStart"
            """).ToListAsync(ct);

        return new BoardFigures(months, totals.CustomerCount, totals.RefundedOrderCount, totals.RefundedAmountCents);
    }

    private sealed record BoardTotalsRow(int CustomerCount, int RefundedOrderCount, int RefundedAmountCents);

    private const int TrendDays = 7;
    private const int MonthlyTrendMonths = 7;

    private static List<int> ZeroTrend() => [.. new int[TrendDays]];

    private static List<int> ZeroMonthlyTrend() => [.. new int[MonthlyTrendMonths]];

    private sealed record SummaryCountsRow(int ProductCount, int LandingPageCount, int TotalPageViews, int SubscriberCount);
}
