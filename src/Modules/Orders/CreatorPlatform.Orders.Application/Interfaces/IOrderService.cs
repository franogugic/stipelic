using CreatorPlatform.Orders.Application.Dtos;

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

    Task<OrderSummaryDto> GetSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct);

    Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(
        string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct);

    Task<HomeSummaryDto> GetHomeSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct);
}
