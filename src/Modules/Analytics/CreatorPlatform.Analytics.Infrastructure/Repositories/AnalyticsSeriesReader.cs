using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Analytics.Infrastructure.Repositories;

/// <summary>The analytics series reads. Buckets are taken in the creator's timezone: each event's timestamptz is
/// turned into local wall-clock time (AT TIME ZONE) and truncated to the hour / day / month, so the bucket key is the
/// local bucket start. Every read is one grouped pass over one source, bounded by the indexed (LandingPageId,
/// ViewedAt / CapturedAt) and (CreatorId, …) columns.</summary>
public sealed class AnalyticsSeriesReader : IAnalyticsSeriesReader
{
    private readonly CreatorPlatformDbContext _context;

    public AnalyticsSeriesReader(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<SeriesCreatorRow?> GetCreatorAsync(string slug, int ownerUserId, CancellationToken ct) =>
        (await _context.Database.SqlQuery<SeriesCreatorRow>($"""
            SELECT c."Id" AS "Id", s."Timezone" AS "Timezone", c."DefaultCurrency" AS "Currency"
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

    public async Task<SeriesReadResult> ReadAsync(
        SeriesTarget target, string timezone, string step, DateTimeOffset from, DateTimeOffset to,
        SeriesRange current, SeriesRange? previous, CancellationToken ct)
    {
        var pageIds = target.LandingPageIds;
        var captureProductId = target.CaptureProductId;
        var orderPageId = target.OrderLandingPageId;
        var orderProductId = target.OrderProductId;
        var currentFrom = current.From;
        var currentTo = current.To;
        var previousFrom = previous?.From;
        var previousTo = previous?.To;

        // Sequential on purpose: one scoped DbContext allows no concurrent queries.
        var views = await _context.Database.SqlQuery<BucketRow>($"""
            SELECT date_trunc({step}, pv."ViewedAt" AT TIME ZONE {timezone}) AS "Bucket", COUNT(*)::bigint AS "Value", 0::bigint AS "Extra"
            FROM analytics.page_views pv
            WHERE pv."LandingPageId" = ANY({pageIds}) AND pv."ViewedAt" >= {from} AND pv."ViewedAt" < {to}
            GROUP BY 1
            """).AsNoTracking().ToListAsync(ct);

        var emails = await _context.Database.SqlQuery<BucketRow>($"""
            SELECT date_trunc({step}, ec."CapturedAt" AT TIME ZONE {timezone}) AS "Bucket", COUNT(DISTINCT ec."Email")::bigint AS "Value", 0::bigint AS "Extra"
            FROM analytics.email_captures ec
            WHERE (ec."LandingPageId" = ANY({pageIds}) OR ({captureProductId}::int IS NOT NULL AND ec."ProductId" = {captureProductId}))
              AND ec."CapturedAt" >= {from} AND ec."CapturedAt" < {to}
            GROUP BY 1
            """).AsNoTracking().ToListAsync(ct);

        var sales = await _context.Database.SqlQuery<BucketRow>($"""
            SELECT date_trunc({step}, COALESCE(o."PaidAt", o."CreatedAt") AT TIME ZONE {timezone}) AS "Bucket",
                   COUNT(*)::bigint AS "Value", COALESCE(SUM(o."AmountCents"), 0)::bigint AS "Extra"
            FROM orders.orders o
            WHERE o."Status" = 'Paid'
              AND ({orderPageId}::int IS NULL OR o."LandingPageId" = {orderPageId})
              AND ({orderProductId}::int IS NULL OR o."ProductId" = {orderProductId})
              AND COALESCE(o."PaidAt", o."CreatedAt") >= {from} AND COALESCE(o."PaidAt", o."CreatedAt") < {to}
            GROUP BY 1
            """).AsNoTracking().ToListAsync(ct);

        var totals = (await _context.Database.SqlQuery<TotalsRow>($"""
            SELECT
                (SELECT COUNT(*) FILTER (WHERE ({currentFrom}::timestamptz IS NULL OR pv."ViewedAt" >= {currentFrom}) AND pv."ViewedAt" < {currentTo})
                 FROM analytics.page_views pv WHERE pv."LandingPageId" = ANY({pageIds}))::bigint AS "Views",
                (SELECT COUNT(*) FILTER (WHERE {previousFrom}::timestamptz IS NOT NULL AND pv."ViewedAt" >= {previousFrom} AND pv."ViewedAt" < {previousTo})
                 FROM analytics.page_views pv WHERE pv."LandingPageId" = ANY({pageIds}))::bigint AS "PreviousViews",
                (SELECT COUNT(DISTINCT ec."Email") FILTER (WHERE ({currentFrom}::timestamptz IS NULL OR ec."CapturedAt" >= {currentFrom}) AND ec."CapturedAt" < {currentTo})
                 FROM analytics.email_captures ec
                 WHERE ec."LandingPageId" = ANY({pageIds}) OR ({captureProductId}::int IS NOT NULL AND ec."ProductId" = {captureProductId}))::bigint AS "Emails",
                (SELECT COUNT(DISTINCT ec."Email") FILTER (WHERE {previousFrom}::timestamptz IS NOT NULL AND ec."CapturedAt" >= {previousFrom} AND ec."CapturedAt" < {previousTo})
                 FROM analytics.email_captures ec
                 WHERE ec."LandingPageId" = ANY({pageIds}) OR ({captureProductId}::int IS NOT NULL AND ec."ProductId" = {captureProductId}))::bigint AS "PreviousEmails",
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
                ) p
            ) o
            """).AsNoTracking().ToListAsync(ct)).Single();

        var buckets = new Dictionary<DateTime, SeriesValues>();
        SeriesValues At(DateTime key) => buckets.GetValueOrDefault(key) ?? new SeriesValues(0, 0, 0, 0);
        foreach (var row in views)
            buckets[row.Bucket] = At(row.Bucket) with { Views = row.Value };
        foreach (var row in emails)
            buckets[row.Bucket] = At(row.Bucket) with { Emails = row.Value };
        foreach (var row in sales)
            buckets[row.Bucket] = At(row.Bucket) with { Sales = row.Value, RevenueCents = row.Extra };

        return new SeriesReadResult(
            buckets,
            new SeriesValues(totals.Views, totals.Emails, totals.Sales, totals.RevenueCents),
            previous is null ? null : new SeriesValues(totals.PreviousViews, totals.PreviousEmails, totals.PreviousSales, totals.PreviousRevenueCents));
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

    private sealed record BucketRow(DateTime Bucket, long Value, long Extra);

    private sealed record TotalsRow(
        long Views, long PreviousViews, long Emails, long PreviousEmails,
        long Sales, long RevenueCents, long PreviousSales, long PreviousRevenueCents);
}
