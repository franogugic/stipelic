using CreatorPlatform.Analytics.Domain.PageViews;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Domain.Contacts;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Domain.Access;
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

    public async Task<Order?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct)
    {
        return await _context
            .Set<Order>()
            .FromSqlInterpolated($"""
                SELECT * FROM orders.orders
                WHERE "PublicId" = {publicId}
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

    public async Task<Order?> GetByStripePaymentIntentIdForUpdateAsync(string stripePaymentIntentId, CancellationToken ct)
    {
        return await _context
            .Set<Order>()
            .FromSqlInterpolated($"""
                SELECT * FROM orders.orders
                WHERE "StripePaymentIntentId" = {stripePaymentIntentId}
                LIMIT 1
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug,
        int ownerUserId,
        OrderListFilter filter,
        OrderListCursor? after,
        int limit,
        CancellationToken ct)
    {
        var query = Filtered(creatorSlug, ownerUserId, filter, includeStatus: true);

        // With a period the rows are ordered by the counted date (the period rule), else by when they were placed.
        if (filter.Period is not null)
        {
            if (after is not null)
            {
                var cursorAt = after.At;
                var cursorId = after.PublicId;
                query = query.Where(x => x.CountedAt < cursorAt || (x.CountedAt == cursorAt && x.o.PublicId.CompareTo(cursorId) < 0));
            }
            query = query.OrderByDescending(x => x.CountedAt).ThenByDescending(x => x.o.PublicId);
        }
        else
        {
            if (after is not null)
            {
                var cursorCreatedAt = after.At;
                var cursorId = after.PublicId;
                // Mirrors OrderKeysetCursor.IsBeforeCursor (see its tests) — inlined because EF Core
                // can't translate a call to an extracted predicate into SQL.
                query = query.Where(x =>
                    x.o.CreatedAt < cursorCreatedAt ||
                    (x.o.CreatedAt == cursorCreatedAt && x.o.PublicId.CompareTo(cursorId) < 0));
            }
            query = query.OrderByDescending(x => x.o.CreatedAt).ThenByDescending(x => x.o.PublicId);
        }

        return await query
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
                // A Paid order's fee and net are what it keeps after a partial refund.
                x.o.Status == OrderStatus.Paid ? x.o.KeptPlatformFeeCents : x.o.PlatformFeeCents,
                x.o.Status == OrderStatus.Paid ? x.o.KeptAmountCents - x.o.KeptPlatformFeeCents : x.o.AmountCents - x.o.PlatformFeeCents,
                x.LandingPageTitle,
                x.DownloadCount ?? 0,
                x.LastDownloadedAt,
                x.CountedAt,
                x.o.Status == OrderStatus.Refunded ? x.o.UpdatedAt : null,
                x.o.RefundedCents))
            .ToListAsync(ct);
    }

    public async Task<OrderStatusCountsDto> GetStatusCountsAsync(
        string creatorSlug, int ownerUserId, OrderListFilter filter, CancellationToken ct)
    {
        var counts = await Filtered(creatorSlug, ownerUserId, filter, includeStatus: false)
            .GroupBy(x => x.o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Of(OrderStatus status) => counts.Where(c => c.Status == status).Sum(c => c.Count);
        return new OrderStatusCountsDto(
            counts.Sum(c => c.Count), Of(OrderStatus.Paid), Of(OrderStatus.Pending), Of(OrderStatus.Failed), Of(OrderStatus.Refunded));
    }

    public async Task<int?> GetProductIdAsync(int creatorId, Guid productPublicId, CancellationToken ct) =>
        await _context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.CreatorId == creatorId && p.PublicId == productPublicId)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);

    // A member-init projection (not a positional record), so EF can keep composing filters and ordering on it.
    private sealed class FilteredOrder
    {
        public required Order o { get; init; }
        public required string ProductName { get; init; }
        public string? LandingPageTitle { get; init; }
        public int? DownloadCount { get; init; }
        public DateTimeOffset? LastDownloadedAt { get; init; }
        public DateTimeOffset CountedAt { get; init; }
    }

    /// <summary>The caller's orders under <paramref name="filter"/> (the status only with <paramref name="includeStatus"/>),
    /// each with its counted date: PaidAt (else CreatedAt) when Paid, the refund time (UpdatedAt) when Refunded,
    /// CreatedAt when Pending or Failed.</summary>
    private IQueryable<FilteredOrder> Filtered(string creatorSlug, int ownerUserId, OrderListFilter filter, bool includeStatus)
    {
        var productPublicId = filter.ProductPublicId;
        var landingPagePublicId = filter.LandingPagePublicId;
        var status = includeStatus ? filter.Status : null;

        // Left join on landing pages — Order.LandingPageId is nullable (direct-link/no-referrer
        // orders have none), so this must not drop rows the way an inner join would.
        // Filtered server-side (not client-side) so a status/product filter narrows the full order
        // set, not just whatever page happens to be loaded — keyset pagination still walks the
        // filtered result set correctly since the cursor condition is applied on top of it. Product is
        // filtered by PublicId directly on the already-joined `p` — no extra round trip to resolve it
        // to an internal id first.
        var query =
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            join c in _context.Set<Creator>().AsNoTracking() on o.CreatorId equals c.Id
            join lp in _context.Set<LandingPage>().AsNoTracking() on o.LandingPageId equals (int?)lp.Id into lpJoin
            from lp in lpJoin.DefaultIfEmpty()
            // At most one grant per order (unique index), so this left join never multiplies rows.
            join g in _context.Set<AccessGrant>().AsNoTracking() on (int?)o.Id equals g.OrderId into gJoin
            from g in gJoin.DefaultIfEmpty()
            where c.Slug == creatorSlug
                && c.OwnerUserId == ownerUserId
                && (!productPublicId.HasValue || p.PublicId == productPublicId.Value)
                && (!landingPagePublicId.HasValue || lp.PublicId == landingPagePublicId.Value)
                && (!status.HasValue || o.Status == status.Value)
            select new FilteredOrder
            {
                o = o,
                ProductName = p.Name,
                LandingPageTitle = (string?)lp.Title,
                DownloadCount = (int?)g.DownloadCount,
                LastDownloadedAt = (DateTimeOffset?)g.LastDownloadedAt,
                CountedAt = o.Status == OrderStatus.Paid ? (o.PaidAt ?? o.CreatedAt)
                    : o.Status == OrderStatus.Refunded ? o.UpdatedAt
                    : o.CreatedAt,
            };

        if (filter.CustomerSearch is { } customerSearch)
        {
            // Case-insensitive substring match on email OR name. Written as lower(column) LIKE '%term%' so it
            // matches the GIN trigram indexes on lower("Email") / lower("Name") exactly (see migration
            // AddOrderCustomerSearchIndexes). The term's own %, _ and \ are escaped so they match literally.
            var pattern = LikePatterns.Contains(customerSearch.ToLowerInvariant());
            query = query.Where(x =>
                (x.o.Email != null && EF.Functions.Like(x.o.Email.ToLower(), pattern, LikePatterns.EscapeCharacter))
                || (x.o.Name != null && EF.Functions.Like(x.o.Name.ToLower(), pattern, LikePatterns.EscapeCharacter)));
        }

        if (filter.Period is { } period)
        {
            var from = period.From;
            var to = period.To;
            query = query.Where(x => (from == null || x.CountedAt >= from) && x.CountedAt < to);
        }

        return query;
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
                COALESCE(SUM(o."KeptAmountCents") FILTER (WHERE o."Status" = 'Paid'), 0)::int AS "TotalPaidAmountCents",
                COALESCE(SUM(o."KeptPlatformFeeCents") FILTER (WHERE o."Status" = 'Paid'), 0)::int AS "TotalPlatformFeeCents",
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

    public async Task<OrderPeriodContext?> GetPeriodContextAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        var row = (await _context.Database.SqlQuery<PeriodContextRow>($"""
            SELECT c."Id" AS "Id", s."Timezone" AS "Timezone", c."DefaultCurrency" AS "Currency", c."CreatedAt" AS "CreatedAt"
            FROM creators.creators c
            LEFT JOIN creators.creator_settings s ON s."CreatorId" = c."Id"
            WHERE c."Slug" = {creatorSlug} AND c."OwnerUserId" = {ownerUserId} AND c."Status" <> 'Disabled'
            """).AsNoTracking().ToListAsync(ct)).SingleOrDefault();

        return row is null ? null : new OrderPeriodContext(row.Id, CreatorTimeZone.Resolve(row.Timezone), row.Currency, row.CreatedAt);
    }

    public async Task<OrderPeriodRead> GetPeriodSummaryAsync(
        int creatorId, string timezone, AnalyticsPeriodPlan plan, CancellationToken ct, int? productId = null)
    {
        var from = plan.TotalsFrom;
        var to = plan.TotalsTo;
        var previousFrom = plan.PreviousTotalsFrom;
        var previousTo = plan.PreviousTotalsTo;

        // One pass over the creator's orders (the (CreatorId, …) indexes), a FILTER per figure and window. Every count
        // uses the counted date: PaidAt (else CreatedAt) for Paid, the refund time (UpdatedAt) for Refunded, CreatedAt
        // for Pending and Failed — the same rule as the orders list.
        var rows = await _context.Database.SqlQuery<PeriodTotalsRow>($"""
            WITH counted AS (
                SELECT o."Status", o."AmountCents", o."KeptAmountCents", o."KeptPlatformFeeCents",
                       CASE o."Status" WHEN 'Paid' THEN COALESCE(o."PaidAt", o."CreatedAt")
                                       WHEN 'Refunded' THEN o."UpdatedAt"
                                       ELSE o."CreatedAt" END AS "At"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId} AND ({productId}::int IS NULL OR o."ProductId" = {productId})
            ),
            windows AS (
                SELECT 'current' AS "Window", {from}::timestamptz AS "From", {to}::timestamptz AS "To"
                UNION ALL
                SELECT 'previous', {previousFrom}::timestamptz, {previousTo}::timestamptz WHERE {previousTo}::timestamptz IS NOT NULL
            )
            SELECT w."Window",
                (COUNT(*) FILTER (WHERE c."Status" = 'Paid'))::int AS "PaidOrderCount",
                COALESCE(SUM(c."KeptAmountCents") FILTER (WHERE c."Status" = 'Paid'), 0)::bigint AS "TotalPaidAmountCents",
                COALESCE(SUM(c."KeptPlatformFeeCents") FILTER (WHERE c."Status" = 'Paid'), 0)::bigint AS "TotalPlatformFeeCents",
                (COUNT(*) FILTER (WHERE c."Status" = 'Refunded'))::int AS "RefundedOrderCount",
                COALESCE(SUM(c."AmountCents") FILTER (WHERE c."Status" = 'Refunded'), 0)::bigint AS "RefundedAmountCents",
                (COUNT(*) FILTER (WHERE c."Status" = 'Pending'))::int AS "PendingOrderCount",
                (COUNT(*) FILTER (WHERE c."Status" = 'Failed'))::int AS "FailedOrderCount"
            FROM windows w
            LEFT JOIN counted c ON (w."From" IS NULL OR c."At" >= w."From") AND c."At" < w."To"
            GROUP BY w."Window"
            """).AsNoTracking().ToListAsync(ct);

        static OrderPeriodTotalsRow Totals(PeriodTotalsRow row) => new(
            row.PaidOrderCount, row.TotalPaidAmountCents, row.TotalPlatformFeeCents, row.RefundedOrderCount,
            row.RefundedAmountCents, row.PendingOrderCount, row.FailedOrderCount);
        var totals = Totals(rows.Single(row => row.Window == "current"));
        var previous = rows.SingleOrDefault(row => row.Window == "previous") is { } previousRow ? Totals(previousRow) : null;

        // The buckets: one grouped pass for paid orders (by when they were paid) and one for refunds (by the refund's
        // time), each binned with width_bucket over the plan's local bucket starts (see AnalyticsSeriesReader).
        var starts = plan.Buckets;
        var edges = AnalyticsPeriodPlan.ToEdges(starts);
        var bucketsFrom = plan.TotalsFrom ?? plan.BucketsFrom;
        var bucketRows = await _context.Database.SqlQuery<PeriodBucketRow>($"""
            SELECT 'paid' AS "Kind",
                   width_bucket(extract(epoch FROM (COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE {timezone}))::float8, {edges}) AS "Bucket",
                   COALESCE(SUM(o."KeptAmountCents"), 0)::bigint AS "GrossCents",
                   COALESCE(SUM(o."KeptPlatformFeeCents"), 0)::bigint AS "FeeCents",
                   COUNT(*)::int AS "Count"
            FROM orders.orders o
            WHERE o."CreatorId" = {creatorId} AND ({productId}::int IS NULL OR o."ProductId" = {productId}) AND o."Status" = 'Paid'
              AND COALESCE(o."PaidAt", o."CreatedAt") >= {bucketsFrom} AND COALESCE(o."PaidAt", o."CreatedAt") < {to}
            GROUP BY 2
            UNION ALL
            SELECT 'refunded' AS "Kind",
                   width_bucket(extract(epoch FROM (o."UpdatedAt" AT TIME ZONE {timezone}))::float8, {edges}) AS "Bucket",
                   COALESCE(SUM(o."AmountCents"), 0)::bigint, 0::bigint,
                   COUNT(*)::int
            FROM orders.orders o
            WHERE o."CreatorId" = {creatorId} AND ({productId}::int IS NULL OR o."ProductId" = {productId}) AND o."Status" = 'Refunded'
              AND o."UpdatedAt" >= {bucketsFrom} AND o."UpdatedAt" < {to}
            GROUP BY 2
            """).AsNoTracking().ToListAsync(ct);

        var buckets = new Dictionary<DateTime, OrderBucketValues>();
        foreach (var row in bucketRows)
        {
            // width_bucket numbers the bins from 1; 0 is an event before the first start, excluded by the range above.
            if (row.Bucket < 1 || row.Bucket > starts.Count)
                continue;
            var key = starts[row.Bucket - 1];
            var current = buckets.GetValueOrDefault(key) ?? new OrderBucketValues(0, 0, 0);
            buckets[key] = row.Kind == "paid"
                ? current with { GrossCents = row.GrossCents, FeeCents = row.FeeCents, PaidCount = row.Count }
                : current with { RefundedCount = row.Count, RefundedCents = row.GrossCents };
        }

        return new OrderPeriodRead(totals, buckets, previous);
    }

    private sealed record PeriodContextRow(int Id, string? Timezone, string Currency, DateTimeOffset CreatedAt);

    private sealed record PeriodBucketRow(string Kind, int Bucket, long GrossCents, long FeeCents, int Count);

    private sealed record PeriodTotalsRow(
        string Window, int PaidOrderCount, long TotalPaidAmountCents, long TotalPlatformFeeCents, int RefundedOrderCount,
        long RefundedAmountCents, int PendingOrderCount, int FailedOrderCount);

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
                SELECT o."Status", o."KeptAmountCents", o."KeptPlatformFeeCents",
                       date_trunc('month', COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE {timezone}) AS "PaidMonth",
                       date_trunc('month', o."UpdatedAt" AT TIME ZONE {timezone}) AS "UpdatedMonth"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId} AND o."Status" IN ('Paid', 'Refunded')
            )
            SELECT to_char(m."MonthStart", 'YYYY-MM') AS "Month",
                   COALESCE(SUM(lo."KeptAmountCents") FILTER (WHERE lo."Status" = 'Paid' AND lo."PaidMonth" = m."MonthStart"), 0)::bigint AS "GrossCents",
                   COALESCE(SUM(lo."KeptPlatformFeeCents") FILTER (WHERE lo."Status" = 'Paid' AND lo."PaidMonth" = m."MonthStart"), 0)::bigint AS "FeeCents",
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

    public async Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(
        int landingPageId, StatsPeriods periods, CancellationToken ct)
    {
        var row = (await _context.Database.SqlQuery<SalesByPeriodRow>($"""
            SELECT
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.StartOfToday}))::int AS "TodayCount",
                COALESCE(SUM(o."KeptAmountCents") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.StartOfToday}), 0)::bigint AS "TodayRevenueCents",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.Last7DaysFrom}))::int AS "Last7DaysCount",
                COALESCE(SUM(o."KeptAmountCents") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.Last7DaysFrom}), 0)::bigint AS "Last7DaysRevenueCents",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.Last30DaysFrom}))::int AS "Last30DaysCount",
                COALESCE(SUM(o."KeptAmountCents") FILTER (WHERE o."Status" = 'Paid' AND o."PaidAt" >= {periods.Last30DaysFrom}), 0)::bigint AS "Last30DaysRevenueCents",
                (COUNT(o."Id") FILTER (WHERE o."Status" = 'Paid'))::int AS "AllTimeCount",
                COALESCE(SUM(o."KeptAmountCents") FILTER (WHERE o."Status" = 'Paid'), 0)::bigint AS "AllTimeRevenueCents",
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
                TotalRevenueCents = g.Sum(o => o.KeptAmountCents)
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
                TotalPaidAmountCents = g.Sum(o => o.Status == OrderStatus.Paid ? o.KeptAmountCents : 0),
                ThisMonthRevenueCents = g.Sum(o => o.Status == OrderStatus.Paid && o.PaidAt >= monthStart ? o.KeptAmountCents : 0),
                TotalPlatformFeeCents = g.Sum(o => o.Status == OrderStatus.Paid ? o.KeptPlatformFeeCents : 0),
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
                o.KeptPlatformFeeCents,
                o.KeptAmountCents - o.KeptPlatformFeeCents,
                null,
                0,
                null,
                o.PaidAt ?? o.CreatedAt,
                null,
                o.RefundedCents)
        ).Take(5).ToListAsync(ct);

        // Top product by paid revenue (all-time).
        var topRow = await (
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            where o.CreatorId == creator.Id && o.Status == OrderStatus.Paid
            group new { o, p } by new { o.ProductId, p.Name } into g
            select new { g.Key.Name, Total = g.Sum(x => x.o.KeptAmountCents) }
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
            .Select(o => new { o.PaidAt, AmountCents = o.KeptAmountCents })
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
            .Select(o => new { o.PaidAt, AmountCents = o.KeptAmountCents })
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
