using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Shared.Application.Analytics;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Analytics.Infrastructure.Repositories;

/// <summary>The analytics series reads. Buckets are taken in the creator's timezone: each event's timestamptz is
/// turned into local wall-clock time (AT TIME ZONE) and binned with width_bucket over the plan's local bucket starts,
/// so one expression serves hours, days, weeks and months, and a DST change never moves an event to another bucket.
/// Every read is one grouped pass over one source, bounded by the indexed (LandingPageId, ViewedAt / CapturedAt) and
/// (CreatorId, …) columns.</summary>
public sealed class AnalyticsSeriesReader : IAnalyticsSeriesReader
{
    private readonly CreatorPlatformDbContext _context;

    public AnalyticsSeriesReader(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<SeriesCreatorRow?> GetCreatorAsync(string slug, int ownerUserId, CancellationToken ct) =>
        (await _context.Database.SqlQuery<SeriesCreatorRow>($"""
            SELECT c."Id" AS "Id", s."Timezone" AS "Timezone", c."DefaultCurrency" AS "Currency", c."CreatedAt" AS "CreatedAt"
            FROM creators.creators c
            LEFT JOIN creators.creator_settings s ON s."CreatorId" = c."Id"
            WHERE c."Slug" = {slug} AND c."OwnerUserId" = {ownerUserId} AND c."Status" <> 'Disabled'
            """).AsNoTracking().ToListAsync(ct)).SingleOrDefault();

    public async Task<SeriesItemRow?> GetPageAsync(int creatorId, Guid publicId, CancellationToken ct) =>
        (await _context.Database.SqlQuery<SeriesItemRow>($"""
            SELECT lp."Id" AS "Id", lp."CreatedAt" AS "CreatedAt"
            FROM landing_pages.landing_pages lp
            WHERE lp."PublicId" = {publicId} AND lp."CreatorId" = {creatorId}
            """).AsNoTracking().ToListAsync(ct)).SingleOrDefault();

    public async Task<SeriesItemRow?> GetProductAsync(int creatorId, Guid publicId, CancellationToken ct) =>
        (await _context.Database.SqlQuery<SeriesItemRow>($"""
            SELECT p."Id" AS "Id", p."CreatedAt" AS "CreatedAt"
            FROM products.products p
            WHERE p."PublicId" = {publicId} AND p."CreatorId" = {creatorId}
            """).AsNoTracking().ToListAsync(ct)).SingleOrDefault();

    public async Task<int[]> GetProductPageIdsAsync(int productId, CancellationToken ct) =>
        [.. await _context.Database.SqlQuery<int>($"""
            SELECT lp."Id" AS "Value" FROM landing_pages.landing_pages lp WHERE lp."ProductId" = {productId}
            """).ToListAsync(ct)];

    public async Task<int[]> GetCreatorPageIdsAsync(int creatorId, CancellationToken ct) =>
        [.. await _context.Database.SqlQuery<int>($"""
            SELECT lp."Id" AS "Value" FROM landing_pages.landing_pages lp WHERE lp."CreatorId" = {creatorId}
            """).ToListAsync(ct)];

    public async Task<SeriesReadResult> ReadAsync(
        SeriesTarget target, string timezone, IReadOnlyList<DateTime> bucketStarts, DateTimeOffset from, DateTimeOffset to,
        SeriesRange current, SeriesRange? previous, CancellationToken ct)
    {
        var edges = AnalyticsPeriodPlan.ToEdges(bucketStarts);
        var workspaceCreatorId = target.WorkspaceCreatorId;
        var pageIds = target.LandingPageIds;
        var captureProductId = target.CaptureProductId;
        var orderPageId = target.OrderLandingPageId;
        var orderProductId = target.OrderProductId;
        var orderCreatorId = target.WorkspaceCreatorId;
        var currentFrom = current.From;
        var currentTo = current.To;
        var previousFrom = previous?.From;
        var previousTo = previous?.To;

        // Sequential on purpose: one scoped DbContext allows no concurrent queries.
        var views = await _context.Database.SqlQuery<BucketRow>($"""
            SELECT width_bucket(extract(epoch FROM (pv."ViewedAt" AT TIME ZONE {timezone}))::float8, {edges}) AS "Bucket", COUNT(*)::bigint AS "Value", 0::bigint AS "Extra"
            FROM analytics.page_views pv
            WHERE pv."LandingPageId" = ANY({pageIds}) AND pv."ViewedAt" >= {from} AND pv."ViewedAt" < {to}
            GROUP BY 1
            """).AsNoTracking().ToListAsync(ct);

        // A workspace counts new contacts (first capture), a page or product the distinct emails captured on it.
        var emails = workspaceCreatorId is null
            ? await _context.Database.SqlQuery<BucketRow>($"""
                SELECT width_bucket(extract(epoch FROM (ec."CapturedAt" AT TIME ZONE {timezone}))::float8, {edges}) AS "Bucket", COUNT(DISTINCT ec."Email")::bigint AS "Value", 0::bigint AS "Extra"
                FROM analytics.email_captures ec
                WHERE (ec."LandingPageId" = ANY({pageIds}) OR ({captureProductId}::int IS NOT NULL AND ec."ProductId" = {captureProductId}))
                  AND ec."CapturedAt" >= {from} AND ec."CapturedAt" < {to}
                GROUP BY 1
                """).AsNoTracking().ToListAsync(ct)
            : await _context.Database.SqlQuery<BucketRow>($"""
                SELECT width_bucket(extract(epoch FROM (cs."FirstCapturedAt" AT TIME ZONE {timezone}))::float8, {edges}) AS "Bucket", COUNT(*)::bigint AS "Value", 0::bigint AS "Extra"
                FROM marketing.contact_summaries cs
                WHERE cs."CreatorId" = {workspaceCreatorId} AND cs."FirstCapturedAt" >= {from} AND cs."FirstCapturedAt" < {to}
                GROUP BY 1
                """).AsNoTracking().ToListAsync(ct);

        var sales = await _context.Database.SqlQuery<BucketRow>($"""
            SELECT width_bucket(extract(epoch FROM (COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE {timezone}))::float8, {edges}) AS "Bucket",
                   COUNT(*)::bigint AS "Value", COALESCE(SUM(o."AmountCents"), 0)::bigint AS "Extra"
            FROM orders.orders o
            WHERE o."Status" = 'Paid'
              AND ({orderPageId}::int IS NULL OR o."LandingPageId" = {orderPageId})
              AND ({orderProductId}::int IS NULL OR o."ProductId" = {orderProductId})
              AND ({orderCreatorId}::int IS NULL OR o."CreatorId" = {orderCreatorId})
              AND COALESCE(o."PaidAt", o."CreatedAt") >= {from} AND COALESCE(o."PaidAt", o."CreatedAt") < {to}
            GROUP BY 1
            """).AsNoTracking().ToListAsync(ct);

        // Downloads (R6.2 events): a product's links, the links that came from a page, or every link of the workspace.
        var downloads = await _context.Database.SqlQuery<BucketRow>($"""
            SELECT width_bucket(extract(epoch FROM (de."DownloadedAt" AT TIME ZONE {timezone}))::float8, {edges}) AS "Bucket", COUNT(*)::bigint AS "Value", 0::bigint AS "Extra"
            FROM products.download_events de
            WHERE ({orderPageId}::int IS NULL OR de."LandingPageId" = {orderPageId})
              AND ({orderProductId}::int IS NULL OR de."ProductId" = {orderProductId})
              AND ({orderCreatorId}::int IS NULL OR de."CreatorId" = {orderCreatorId})
              AND de."DownloadedAt" >= {from} AND de."DownloadedAt" < {to}
            GROUP BY 1
            """).AsNoTracking().ToListAsync(ct);

        var downloadTotals = (await _context.Database.SqlQuery<DownloadTotalsRow>($"""
            SELECT
                (COUNT(*) FILTER (WHERE ({currentFrom}::timestamptz IS NULL OR de."DownloadedAt" >= {currentFrom}) AND de."DownloadedAt" < {currentTo}))::bigint AS "Downloads",
                (COUNT(*) FILTER (WHERE {previousFrom}::timestamptz IS NOT NULL AND de."DownloadedAt" >= {previousFrom} AND de."DownloadedAt" < {previousTo}))::bigint AS "PreviousDownloads"
            FROM products.download_events de
            WHERE ({orderPageId}::int IS NULL OR de."LandingPageId" = {orderPageId})
              AND ({orderProductId}::int IS NULL OR de."ProductId" = {orderProductId})
              AND ({orderCreatorId}::int IS NULL OR de."CreatorId" = {orderCreatorId})
            """).AsNoTracking().ToListAsync(ct)).Single();

        var totals = (await _context.Database.SqlQuery<TotalsRow>($"""
            SELECT
                (SELECT COUNT(*) FILTER (WHERE ({currentFrom}::timestamptz IS NULL OR pv."ViewedAt" >= {currentFrom}) AND pv."ViewedAt" < {currentTo})
                 FROM analytics.page_views pv WHERE pv."LandingPageId" = ANY({pageIds}))::bigint AS "Views",
                (SELECT COUNT(*) FILTER (WHERE {previousFrom}::timestamptz IS NOT NULL AND pv."ViewedAt" >= {previousFrom} AND pv."ViewedAt" < {previousTo})
                 FROM analytics.page_views pv WHERE pv."LandingPageId" = ANY({pageIds}))::bigint AS "PreviousViews",
                o."Sales", o."RevenueCents", o."PreviousSales", o."PreviousRevenueCents"
            FROM (
                SELECT
                    (COUNT(*) FILTER (WHERE ({currentFrom}::timestamptz IS NULL OR p."At" >= {currentFrom}) AND p."At" < {currentTo}))::bigint AS "Sales",
                    COALESCE(SUM(p."AmountCents") FILTER (WHERE ({currentFrom}::timestamptz IS NULL OR p."At" >= {currentFrom}) AND p."At" < {currentTo}), 0)::bigint AS "RevenueCents",
                    (COUNT(*) FILTER (WHERE {previousFrom}::timestamptz IS NOT NULL AND p."At" >= {previousFrom} AND p."At" < {previousTo}))::bigint AS "PreviousSales",
                    COALESCE(SUM(p."AmountCents") FILTER (WHERE {previousFrom}::timestamptz IS NOT NULL AND p."At" >= {previousFrom} AND p."At" < {previousTo}), 0)::bigint AS "PreviousRevenueCents"
                FROM (
                    SELECT COALESCE(o."PaidAt", o."CreatedAt") AS "At", o."AmountCents"
                    FROM orders.orders o
                    WHERE o."Status" = 'Paid'
                      AND ({orderPageId}::int IS NULL OR o."LandingPageId" = {orderPageId})
                      AND ({orderProductId}::int IS NULL OR o."ProductId" = {orderProductId})
                      AND ({orderCreatorId}::int IS NULL OR o."CreatorId" = {orderCreatorId})
                ) p
            ) o
            """).AsNoTracking().ToListAsync(ct)).Single();

        var emailTotals = await ReadEmailTotalsAsync(target, pageIds, captureProductId, current, previous, ct);

        // width_bucket numbers the bins from 1; 0 would be an event before the first start, which the range filter
        // above already excludes.
        var buckets = new Dictionary<DateTime, SeriesValues>();
        SeriesValues At(DateTime key) => buckets.GetValueOrDefault(key) ?? new SeriesValues(0, 0, 0, 0, 0);
        DateTime? Start(BucketRow row) => row.Bucket >= 1 && row.Bucket <= bucketStarts.Count ? bucketStarts[row.Bucket - 1] : null;
        foreach (var row in views)
            if (Start(row) is { } key) buckets[key] = At(key) with { Views = row.Value };
        foreach (var row in emails)
            if (Start(row) is { } key) buckets[key] = At(key) with { Emails = row.Value };
        foreach (var row in sales)
            if (Start(row) is { } key) buckets[key] = At(key) with { Sales = row.Value, RevenueCents = row.Extra };
        foreach (var row in downloads)
            if (Start(row) is { } key) buckets[key] = At(key) with { Downloads = row.Value };

        return new SeriesReadResult(
            buckets,
            new SeriesValues(totals.Views, emailTotals.Emails, totals.Sales, totals.RevenueCents, downloadTotals.Downloads),
            previous is null
                ? null
                : new SeriesValues(totals.PreviousViews, emailTotals.PreviousEmails, totals.PreviousSales, totals.PreviousRevenueCents,
                    downloadTotals.PreviousDownloads));
    }

    private async Task<EmailTotalsRow> ReadEmailTotalsAsync(
        SeriesTarget target, int[] pageIds, int? captureProductId, SeriesRange current, SeriesRange? previous, CancellationToken ct)
    {
        var currentFrom = current.From;
        var currentTo = current.To;
        var previousFrom = previous?.From;
        var previousTo = previous?.To;

        if (target.WorkspaceCreatorId is { } creatorId)
        {
            return (await _context.Database.SqlQuery<EmailTotalsRow>($"""
                SELECT
                    (COUNT(*) FILTER (WHERE ({currentFrom}::timestamptz IS NULL OR cs."FirstCapturedAt" >= {currentFrom}) AND cs."FirstCapturedAt" < {currentTo}))::bigint AS "Emails",
                    (COUNT(*) FILTER (WHERE {previousFrom}::timestamptz IS NOT NULL AND cs."FirstCapturedAt" >= {previousFrom} AND cs."FirstCapturedAt" < {previousTo}))::bigint AS "PreviousEmails"
                FROM marketing.contact_summaries cs
                WHERE cs."CreatorId" = {creatorId}
                """).AsNoTracking().ToListAsync(ct)).Single();
        }

        return (await _context.Database.SqlQuery<EmailTotalsRow>($"""
            SELECT
                (COUNT(DISTINCT ec."Email") FILTER (WHERE ({currentFrom}::timestamptz IS NULL OR ec."CapturedAt" >= {currentFrom}) AND ec."CapturedAt" < {currentTo}))::bigint AS "Emails",
                (COUNT(DISTINCT ec."Email") FILTER (WHERE {previousFrom}::timestamptz IS NOT NULL AND ec."CapturedAt" >= {previousFrom} AND ec."CapturedAt" < {previousTo}))::bigint AS "PreviousEmails"
            FROM analytics.email_captures ec
            WHERE ec."LandingPageId" = ANY({pageIds}) OR ({captureProductId}::int IS NOT NULL AND ec."ProductId" = {captureProductId})
            """).AsNoTracking().ToListAsync(ct)).Single();
    }

    public async Task<SeriesProductBreakdownRow> GetProductBreakdownAsync(
        int creatorId, SeriesRange range, int limit, CancellationToken ct)
    {
        var from = range.From;
        var to = range.To;
        // One pass over the creator's paid orders: revenue and sales per product (products without revenue, e.g.
        // free ones, are left out), ranked, with the grand total on every row so the rest can be derived.
        var rows = await _context.Database.SqlQuery<ProductBreakdownRow>($"""
            WITH per_product AS (
                SELECT pr."PublicId" AS "PublicId", pr."Name" AS "Name",
                       COUNT(*)::bigint AS "Sales", SUM(o."AmountCents")::bigint AS "RevenueCents"
                FROM orders.orders o
                JOIN products.products pr ON pr."Id" = o."ProductId"
                WHERE o."CreatorId" = {creatorId} AND o."Status" = 'Paid'
                  AND ({from}::timestamptz IS NULL OR COALESCE(o."PaidAt", o."CreatedAt") >= {from})
                  AND COALESCE(o."PaidAt", o."CreatedAt") < {to}
                GROUP BY pr."Id", pr."PublicId", pr."Name"
                HAVING SUM(o."AmountCents") > 0
            ),
            ranked AS (
                SELECT p.*, ROW_NUMBER() OVER (ORDER BY p."RevenueCents" DESC, p."Name", p."PublicId") AS "Rank",
                       (SUM(p."RevenueCents") OVER ())::bigint AS "Total"
                FROM per_product p
            )
            SELECT r."PublicId", r."Name", r."Sales", r."RevenueCents", r."Total" AS "TotalRevenueCents"
            FROM ranked r
            WHERE r."Rank" <= {limit}
            ORDER BY r."Rank"
            """).AsNoTracking().ToListAsync(ct);

        if (rows.Count == 0)
            return new SeriesProductBreakdownRow(0, [], 0);

        var total = rows[0].TotalRevenueCents;
        var listed = rows.Sum(row => row.RevenueCents);
        return new SeriesProductBreakdownRow(
            total,
            rows.Select(row => new SeriesProductRow(row.PublicId, row.Name, row.Sales, row.RevenueCents)).ToList(),
            total - listed);
    }

    public async Task<SeriesProductExtrasRow> GetProductExtrasAsync(int creatorId, int productId, SeriesRange range, CancellationToken ct)
    {
        var from = range.From;
        var to = range.To;
        // Refunds by the time they happened (a refunded order's UpdatedAt); revenue by when it was paid.
        return (await _context.Database.SqlQuery<SeriesProductExtrasRow>($"""
            SELECT
                (COUNT(*) FILTER (WHERE o."ProductId" = {productId} AND o."Status" = 'Refunded'
                    AND ({from}::timestamptz IS NULL OR o."UpdatedAt" >= {from}) AND o."UpdatedAt" < {to}))::int AS "RefundCount",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."ProductId" = {productId} AND o."Status" = 'Refunded'
                    AND ({from}::timestamptz IS NULL OR o."UpdatedAt" >= {from}) AND o."UpdatedAt" < {to}), 0)::bigint AS "RefundedCents",
                COALESCE(SUM(o."AmountCents") FILTER (WHERE o."Status" = 'Paid'
                    AND ({from}::timestamptz IS NULL OR COALESCE(o."PaidAt", o."CreatedAt") >= {from})
                    AND COALESCE(o."PaidAt", o."CreatedAt") < {to}), 0)::bigint AS "CreatorRevenueCents"
            FROM orders.orders o
            WHERE o."CreatorId" = {creatorId}
            """).AsNoTracking().ToListAsync(ct)).Single();
    }

    public async Task<SeriesExtrasRow> GetExtrasAsync(
        int creatorId, int? productId, int[] productPageIds, SeriesRange current, SeriesRange? previous, CancellationToken ct)
    {
        var from = current.From;
        var to = current.To;
        var previousFrom = previous?.From;
        var previousTo = previous?.To;

        // Orders: refunds by refund time (UpdatedAt); buyers by lower(email) of Paid orders, counted by PaidAt.
        var orders = (await _context.Database.SqlQuery<OrderExtrasRow>($"""
            WITH scoped AS (
                SELECT o."Status", o."AmountCents", o."UpdatedAt", lower(o."Email") AS "Buyer", COALESCE(o."PaidAt", o."CreatedAt") AS "At"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId} AND ({productId}::int IS NULL OR o."ProductId" = {productId})
                  AND o."Status" IN ('Paid', 'Refunded')
            ),
            paid AS (SELECT s."Buyer", s."At" FROM scoped s WHERE s."Status" = 'Paid' AND s."Buyer" IS NOT NULL),
            current_buyers AS (
                SELECT DISTINCT p."Buyer" FROM paid p WHERE ({from}::timestamptz IS NULL OR p."At" >= {from}) AND p."At" < {to}
            )
            SELECT
                (SELECT COUNT(*) FROM scoped s WHERE s."Status" = 'Refunded'
                    AND ({from}::timestamptz IS NULL OR s."UpdatedAt" >= {from}) AND s."UpdatedAt" < {to})::int AS "RefundCount",
                (SELECT COALESCE(SUM(s."AmountCents"), 0) FROM scoped s WHERE s."Status" = 'Refunded'
                    AND ({from}::timestamptz IS NULL OR s."UpdatedAt" >= {from}) AND s."UpdatedAt" < {to})::bigint AS "RefundedCents",
                (SELECT COUNT(*) FROM current_buyers)::int AS "Customers",
                (SELECT COUNT(DISTINCT p."Buyer") FROM paid p
                    WHERE {previousFrom}::timestamptz IS NOT NULL AND p."At" >= {previousFrom} AND p."At" < {previousTo})::int AS "PreviousCustomers",
                (SELECT COUNT(*) FROM current_buyers c
                    WHERE {from}::timestamptz IS NOT NULL
                      AND EXISTS (SELECT 1 FROM paid p WHERE p."Buyer" = c."Buyer" AND p."At" < {from}))::int AS "ReturningCustomers"
            """).AsNoTracking().ToListAsync(ct)).Single();

        // Sign-ups: a product's distinct emails captured on its pages (or for it); the workspace's new contacts.
        var signups = productId is null
            ? (await _context.Database.SqlQuery<SignupExtrasRow>($"""
                SELECT COUNT(*)::int AS "Signups",
                       COUNT(*) FILTER (WHERE NOT EXISTS (
                           SELECT 1 FROM marketing.unsubscribes u WHERE u."CreatorId" = cs."CreatorId" AND u."Email" = cs."Email"))::int AS "StillSubscribed"
                FROM marketing.contact_summaries cs
                WHERE cs."CreatorId" = {creatorId}
                  AND ({from}::timestamptz IS NULL OR cs."FirstCapturedAt" >= {from}) AND cs."FirstCapturedAt" < {to}
                """).AsNoTracking().ToListAsync(ct)).Single()
            : (await _context.Database.SqlQuery<SignupExtrasRow>($"""
                WITH emails AS (
                    SELECT DISTINCT ec."Email"
                    FROM analytics.email_captures ec
                    WHERE (ec."LandingPageId" = ANY({productPageIds}) OR ec."ProductId" = {productId})
                      AND ({from}::timestamptz IS NULL OR ec."CapturedAt" >= {from}) AND ec."CapturedAt" < {to}
                )
                SELECT COUNT(*)::int AS "Signups",
                       COUNT(*) FILTER (WHERE NOT EXISTS (
                           SELECT 1 FROM marketing.unsubscribes u WHERE u."CreatorId" = {creatorId} AND u."Email" = e."Email"))::int AS "StillSubscribed"
                FROM emails e
                """).AsNoTracking().ToListAsync(ct)).Single();

        return new SeriesExtrasRow(
            orders.RefundCount, orders.RefundedCents, orders.Customers, orders.PreviousCustomers, orders.ReturningCustomers,
            signups.Signups, signups.StillSubscribed);
    }

    private sealed record OrderExtrasRow(
        int RefundCount, long RefundedCents, int Customers, int PreviousCustomers, int ReturningCustomers);

    private sealed record SignupExtrasRow(int Signups, int StillSubscribed);

    /// <param name="Bucket">The 1-based bin of width_bucket over the bucket starts.</param>
    private sealed record BucketRow(int Bucket, long Value, long Extra);

    private sealed record EmailTotalsRow(long Emails, long PreviousEmails);

    private sealed record DownloadTotalsRow(long Downloads, long PreviousDownloads);

    private sealed record ProductBreakdownRow(Guid PublicId, string Name, long Sales, long RevenueCents, long TotalRevenueCents);

    private sealed record TotalsRow(
        long Views, long PreviousViews, long Sales, long RevenueCents, long PreviousSales, long PreviousRevenueCents);
}
