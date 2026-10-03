using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Domain.Orders;

namespace CreatorPlatform.Orders.Application.Interfaces;

/// <param name="BucketStart">"yyyy-MM-dd" (UTC).</param>
public sealed record TrendBucketRow(string BucketStart, long Value);

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);

    Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct);

    Task<Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct);

    Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct);

    Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug,
        int ownerUserId,
        Guid? productPublicId,
        Guid? landingPagePublicId,
        OrderStatus? status,
        string? customerSearch,
        DateTimeOffset? afterCreatedAt,
        Guid? afterId,
        int limit,
        CancellationToken ct);

    /// <summary>The internal id of the user's active (not Disabled) workspace with this slug; null if none.</summary>
    Task<int?> GetCreatorIdForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>Paid revenue per bucket, one row per bucket from <paramref name="firstBucket"/> to
    /// <paramref name="lastBucket"/> (UTC bucket starts; <paramref name="unit"/> is "day" or "month"), 0 when empty.</summary>
    Task<List<TrendBucketRow>> GetRevenueTrendAsync(
        int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, CancellationToken ct);

    /// <summary>Page views on the creator's landing pages per bucket, same shape as
    /// <see cref="GetRevenueTrendAsync"/>.</summary>
    Task<List<TrendBucketRow>> GetViewsTrendAsync(
        int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, CancellationToken ct);

    /// <summary>True when the slug is an active (not Disabled) workspace owned by the user.</summary>
    Task<bool> CreatorExistsForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct);

    Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(
        string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct);

    /// <summary>Home summary for a creator whose ownership the caller has already checked.</summary>
    Task<HomeSummaryDto> GetHomeSummaryByCreatorIdAsync(int creatorId, CancellationToken ct);
}
