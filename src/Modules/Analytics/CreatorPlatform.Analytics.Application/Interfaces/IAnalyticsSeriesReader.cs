namespace CreatorPlatform.Analytics.Application.Interfaces;

/// <summary>Reads the numbers behind the analytics series straight from the page views, email captures and orders
/// (raw SQL across those schemas, as the other analytics reads do).</summary>
public interface IAnalyticsSeriesReader
{
    /// <summary>The creator owned by this user (not Disabled), with the raw settings timezone and the currency.</summary>
    Task<SeriesCreatorRow?> GetCreatorAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>One of the creator's landing pages (any status). Null when it isn't theirs.</summary>
    Task<SeriesItemRow?> GetPageAsync(int creatorId, Guid publicId, CancellationToken ct);

    /// <summary>One of the creator's products (any status). Null when it isn't theirs.</summary>
    Task<SeriesItemRow?> GetProductAsync(int creatorId, Guid publicId, CancellationToken ct);

    /// <summary>Every landing page that sells or promotes the product, archived included.</summary>
    Task<int[]> GetProductPageIdsAsync(int productId, CancellationToken ct);

    /// <summary>Per-bucket values (keyed by the local bucket start) between <paramref name="from"/> and
    /// <paramref name="to"/>, and the totals of up to two ranges (From null = all time).</summary>
    Task<SeriesReadResult> ReadAsync(
        SeriesTarget target, string timezone, string step, DateTimeOffset from, DateTimeOffset to,
        SeriesRange current, SeriesRange? previous, CancellationToken ct);

    Task<SeriesProductExtrasRow> GetProductExtrasAsync(int creatorId, int productId, SeriesRange range, CancellationToken ct);
}

public sealed record SeriesCreatorRow(int Id, string? Timezone, string Currency);

public sealed record SeriesItemRow(int Id, DateTimeOffset CreatedAt);

/// <summary>What to count. Views and emails come from <paramref name="LandingPageIds"/> (plus captures made for
/// <paramref name="CaptureProductId"/>); sales from orders of <paramref name="OrderLandingPageId"/> or
/// <paramref name="OrderProductId"/>.</summary>
public sealed record SeriesTarget(int[] LandingPageIds, int? CaptureProductId, int? OrderLandingPageId, int? OrderProductId);

public sealed record SeriesRange(DateTimeOffset? From, DateTimeOffset To);

public sealed record SeriesValues(long Views, long Emails, long Sales, long RevenueCents);

public sealed record SeriesReadResult(
    Dictionary<DateTime, SeriesValues> Buckets,
    SeriesValues Current,
    SeriesValues? Previous);

public sealed record SeriesProductExtrasRow(int RefundCount, long RefundedCents, long CreatorRevenueCents);
