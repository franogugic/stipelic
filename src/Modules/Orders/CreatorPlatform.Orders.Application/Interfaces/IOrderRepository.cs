using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Analytics;

namespace CreatorPlatform.Orders.Application.Interfaces;

/// <param name="BucketStart">"yyyy-MM-dd" (UTC).</param>
public sealed record TrendBucketRow(string BucketStart, long Value);

public sealed record OrderPeriodContext(int CreatorId, string Timezone, string Currency, DateTimeOffset CreatedAt);

public sealed record OrderPeriodTotalsRow(
    int PaidOrderCount, long TotalPaidAmountCents, long TotalPlatformFeeCents, int RefundedOrderCount,
    long RefundedAmountCents, int PendingOrderCount, int FailedOrderCount);

/// <param name="Buckets">Keyed by the local bucket start.</param>
public sealed record OrderPeriodRead(OrderPeriodTotalsRow Totals, Dictionary<DateTime, OrderBucketValues> Buckets);

public sealed record OrderBucketValues(long GrossCents, long FeeCents, int RefundedCount);

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

    /// <summary>The creator's analytics timezone, resolved (an IANA name, UTC when unset or unknown).</summary>
    Task<string> GetCreatorTimezoneAsync(int creatorId, CancellationToken ct);

    /// <summary>Paid revenue per bucket, one row per bucket from <paramref name="firstBucket"/> to
    /// <paramref name="lastBucket"/> (UTC bucket starts; <paramref name="unit"/> is "day" or "month"), 0 when empty.</summary>
    Task<List<TrendBucketRow>> GetRevenueTrendAsync(
        int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, string timezone, CancellationToken ct);

    /// <summary>Page views on the creator's landing pages per bucket, same shape as
    /// <see cref="GetRevenueTrendAsync"/>.</summary>
    Task<List<TrendBucketRow>> GetViewsTrendAsync(
        int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, string timezone, CancellationToken ct);

    /// <summary>True when the slug is an active (not Disabled) workspace owned by the user.</summary>
    Task<bool> CreatorExistsForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>The user's active workspace with the settings the period summary needs; null when it isn't theirs.</summary>
    Task<OrderPeriodContext?> GetPeriodContextAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>The totals and chart buckets of <paramref name="plan"/> for a creator whose ownership the caller has
    /// already checked: one aggregate over the creator's orders plus one grouped pass for the buckets.</summary>
    Task<OrderPeriodRead> GetPeriodSummaryAsync(int creatorId, string timezone, AnalyticsPeriodPlan plan, CancellationToken ct);

    /// <summary>One aggregate over the page's orders with a FILTER per period.</summary>
    Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(int landingPageId, StatsPeriods periods, CancellationToken ct);

    Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(
        string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>Home summary for a creator whose ownership the caller has already checked.</summary>
    Task<HomeSummaryDto> GetHomeSummaryByCreatorIdAsync(int creatorId, CancellationToken ct);
}
