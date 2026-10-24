using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Analytics;

namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record OrderPeriodContext(int CreatorId, string Timezone, string Currency, DateTimeOffset CreatedAt);

public sealed record OrderPeriodTotalsRow(
    int PaidOrderCount, long TotalPaidAmountCents, long TotalPlatformFeeCents, int RefundedOrderCount,
    long RefundedAmountCents, int PendingOrderCount, int FailedOrderCount);

/// <param name="Buckets">Keyed by the local bucket start.</param>
/// <param name="Previous">The previous window's totals; null when the plan has none ("all").</param>
public sealed record OrderPeriodRead(
    OrderPeriodTotalsRow Totals, Dictionary<DateTime, OrderBucketValues> Buckets, OrderPeriodTotalsRow? Previous = null);

public sealed record OrderBucketValues(long GrossCents, long FeeCents, int RefundedCount, int PaidCount = 0, long RefundedCents = 0);

/// <summary>A counted-date window (R6.5): From null = all time.</summary>
public sealed record OrderCountedRange(DateTimeOffset? From, DateTimeOffset To);

/// <summary>The repository's list filter. With <paramref name="Period"/>, rows are the orders whose counted date falls in it,
/// ordered by counted date desc; without, every order, ordered by CreatedAt desc.</summary>
public sealed record OrderListFilter(
    Guid? ProductPublicId,
    Guid? LandingPagePublicId,
    OrderStatus? Status,
    string? CustomerSearch,
    OrderCountedRange? Period);

/// <summary>The keyset cursor: the last row's CreatedAt (no period) or CountedAt (with a period), and its public id.</summary>
public sealed record OrderListCursor(DateTimeOffset At, Guid PublicId);

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);

    Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct);

    Task<Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct);

    /// <summary>The order by its PublicId with the row locked (<c>FOR UPDATE</c>) — the webhook's fallback when the
    /// session id was never stored on it (the session's metadata carries the PublicId).</summary>
    Task<Order?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);

    Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct);

    /// <summary>Same, with the row locked (<c>FOR UPDATE</c>) for the caller's transaction — serializes concurrent
    /// refund events for one order.</summary>
    Task<Order?> GetByStripePaymentIntentIdForUpdateAsync(string stripePaymentIntentId, CancellationToken ct);

    Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug,
        int ownerUserId,
        OrderListFilter filter,
        OrderListCursor? after,
        int limit,
        CancellationToken ct);

    /// <summary>Orders per status for <paramref name="filter"/>, ignoring its status (the tabs of the list).</summary>
    Task<OrderStatusCountsDto> GetStatusCountsAsync(string creatorSlug, int ownerUserId, OrderListFilter filter, CancellationToken ct);

    /// <summary>The creator's product with this public id (any status); null when it isn't theirs.</summary>
    Task<int?> GetProductIdAsync(int creatorId, Guid productPublicId, CancellationToken ct);

    /// <summary>The internal id of the user's active (not Disabled) workspace with this slug; null if none.</summary>
    Task<int?> GetCreatorIdForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>The creator's analytics timezone, resolved (an IANA name, UTC when unset or unknown).</summary>
    Task<string> GetCreatorTimezoneAsync(int creatorId, CancellationToken ct);

    /// <summary>True when the slug is an active (not Disabled) workspace owned by the user.</summary>
    Task<bool> CreatorExistsForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>The user's active workspace with the settings the period summary needs; null when it isn't theirs.</summary>
    Task<OrderPeriodContext?> GetPeriodContextAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>The totals and chart buckets of <paramref name="plan"/> for a creator whose ownership the caller has
    /// already checked: one aggregate over the creator's orders plus one grouped pass for the buckets.</summary>
    Task<OrderPeriodRead> GetPeriodSummaryAsync(
        int creatorId, string timezone, AnalyticsPeriodPlan plan, CancellationToken ct, int? productId = null);

    /// <summary>One aggregate over the page's orders with a FILTER per period.</summary>
    Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(int landingPageId, StatsPeriods periods, CancellationToken ct);

    Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(
        string creatorSlug, int ownerUserId, CancellationToken ct);

    /// <summary>Home summary for a creator whose ownership the caller has already checked.</summary>
    Task<HomeSummaryDto> GetHomeSummaryByCreatorIdAsync(int creatorId, CancellationToken ct);
}
