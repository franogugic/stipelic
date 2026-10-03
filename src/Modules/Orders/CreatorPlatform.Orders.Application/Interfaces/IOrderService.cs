using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Shared.Application.Analytics;

namespace CreatorPlatform.Orders.Application.Interfaces;

/// <summary>A validated export: ownership and filters were checked when it was created, so <see cref="Orders"/>
/// can be streamed after the response has started. Enumerate it once.</summary>
public sealed record OrdersExport(string CreatorSlug, IAsyncEnumerable<OrderDto> Orders);

public interface IOrderService
{
    Task<OrdersPageDto> ListAsync(
        string creatorSlug,
        int ownerUserId,
        Guid? productId,
        Guid? landingPageId,
        string? status,
        string? search,
        DateTimeOffset? afterCreatedAt,
        Guid? afterId,
        int limit,
        CancellationToken ct);

    /// <summary>Same filters as <see cref="ListAsync"/>. Validation and the ownership check (404) happen here,
    /// before any output; the orders are then streamed lazily in keyset batches, newest first.</summary>
    Task<OrdersExport> StartExportAsync(
        string creatorSlug,
        int ownerUserId,
        Guid? productId,
        Guid? landingPageId,
        string? status,
        string? search,
        CancellationToken ct);

    /// <summary>Revenue + views series for the dashboard. <paramref name="range"/>: "30d" (30 daily buckets incl.
    /// today), "6m" / "12m" (monthly buckets incl. the current month); null means 30d. 400 for any other value,
    /// 404 when the slug isn't the user's workspace.</summary>
    Task<DashboardTrendsDto> GetDashboardTrendsAsync(string creatorSlug, int ownerUserId, string? range, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct);

    /// <summary>Sales and revenue of a landing page per analytics period. The caller has already checked ownership.</summary>
    Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(int landingPageId, StatsPeriods periods, CancellationToken ct);

    Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(
        string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct);

    /// <summary>404 when the slug isn't the user's workspace — checked before the cache is read.</summary>
    Task<HomeSummaryDto> GetHomeSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>Drops the cached home summary of the caller's own workspace after a change that affects it. A slug
    /// that isn't the caller's is ignored. Not cancellable: the change has already been saved.</summary>
    Task InvalidateHomeSummaryAsync(string creatorSlug, int ownerUserId);
}
