namespace CreatorPlatform.Analytics.Application.Interfaces;

/// <summary>Reads the numbers behind the analytics series straight from the page views, email captures and orders
/// (raw SQL across those schemas, as the other analytics reads do).</summary>
public interface IAnalyticsSeriesReader
{
    /// <summary>The creator owned by this user (not Disabled), with the raw settings timezone, the currency and when
    /// the workspace was created.</summary>
    Task<SeriesCreatorRow?> GetCreatorAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>One of the creator's landing pages (any status). Null when it isn't theirs.</summary>
    Task<SeriesItemRow?> GetPageAsync(int creatorId, Guid publicId, CancellationToken ct);

    /// <summary>One of the creator's products (any status). Null when it isn't theirs.</summary>
    Task<SeriesItemRow?> GetProductAsync(int creatorId, Guid publicId, CancellationToken ct);

    /// <summary>Every landing page that sells or promotes the product, archived included.</summary>
    Task<int[]> GetProductPageIdsAsync(int productId, CancellationToken ct);

    /// <summary>Every landing page of the creator, archived included — the views of a whole workspace.</summary>
    Task<int[]> GetCreatorPageIdsAsync(int creatorId, CancellationToken ct);

    /// <summary>Per-bucket values (keyed by the local bucket start) between <paramref name="from"/> and
    /// <paramref name="to"/>, and the totals of up to two ranges (From null = all time). <paramref name="bucketStarts"/>
    /// are the local bucket starts, oldest first (previous period, then current): every event falls in the last start
    /// at or before its local time.</summary>
    Task<SeriesReadResult> ReadAsync(
        SeriesTarget target, string timezone, IReadOnlyList<DateTime> bucketStarts, DateTimeOffset from, DateTimeOffset to,
        SeriesRange current, SeriesRange? previous, CancellationToken ct);

    /// <summary>Paid revenue and sales per product in the range, best first: at most <paramref name="limit"/> products
    /// with revenue, the period's total revenue and the part of it beyond the listed products. One query.</summary>
    Task<SeriesProductBreakdownRow> GetProductBreakdownAsync(int creatorId, SeriesRange range, int limit, CancellationToken ct);

    Task<SeriesProductExtrasRow> GetProductExtrasAsync(int creatorId, int productId, SeriesRange range, CancellationToken ct);

    /// <summary>The workspace's (<paramref name="productId"/> null) or one product's refunds, customers and sign-ups in
    /// the period (R6.3): two grouped reads, one over the creator's orders and one over the sign-ups.</summary>
    /// <param name="productPageIds">The product's pages (its sign-ups); ignored for the workspace, which counts new contacts.</param>
    Task<SeriesExtrasRow> GetExtrasAsync(
        int creatorId, int? productId, int[] productPageIds, SeriesRange current, SeriesRange? previous, CancellationToken ct);
}

public sealed record SeriesCreatorRow(int Id, string? Timezone, string Currency, DateTimeOffset CreatedAt);

public sealed record SeriesItemRow(int Id, DateTimeOffset CreatedAt);

/// <summary>What to count. Views and emails come from <paramref name="LandingPageIds"/> (plus captures made for
/// <paramref name="CaptureProductId"/>); sales from orders of <paramref name="OrderLandingPageId"/> or
/// <paramref name="OrderProductId"/>. For a whole workspace (<paramref name="WorkspaceCreatorId"/>): views of all the
/// pages, emails = new contacts (first captured in the bucket) and every paid order of the creator.</summary>
public sealed record SeriesTarget(
    int[] LandingPageIds, int? CaptureProductId, int? OrderLandingPageId, int? OrderProductId, int? WorkspaceCreatorId = null);

public sealed record SeriesRange(DateTimeOffset? From, DateTimeOffset To);

/// <param name="Downloads">Download requests through personal links (R6.2 events): the product's links for a product,
/// links that came from the page for a page, every link for a workspace.</param>
public sealed record SeriesValues(long Views, long Emails, long Sales, long RevenueCents, long Downloads);

public sealed record SeriesReadResult(
    Dictionary<DateTime, SeriesValues> Buckets,
    SeriesValues Current,
    SeriesValues? Previous);

public sealed record SeriesProductExtrasRow(int RefundCount, long RefundedCents, long CreatorRevenueCents);

public sealed record SeriesExtrasRow(
    int RefundCount, long RefundedCents, int Customers, int PreviousCustomers, int ReturningCustomers,
    int Signups, int SignupsStillSubscribed);

public sealed record SeriesProductRow(Guid PublicId, string Name, long Sales, long RevenueCents);

/// <param name="TotalRevenueCents">All the creator's product revenue in the range.</param>
/// <param name="OtherRevenueCents">What the listed <paramref name="Products"/> leave out.</param>
public sealed record SeriesProductBreakdownRow(long TotalRevenueCents, List<SeriesProductRow> Products, long OtherRevenueCents);
