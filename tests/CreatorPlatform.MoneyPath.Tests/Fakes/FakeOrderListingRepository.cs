using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Analytics;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

/// <summary>Dedicated fake for OrderService.ListAsync tests — captures the exact args the service
/// passes down (clamped limit, cursor pass-through) and returns a canned page of rows.</summary>
public sealed class FakeOrderListingRepository : IOrderRepository
{
    public List<OrderDto> RowsToReturn { get; set; } = [];

    /// <summary>When set, the fake behaves like the real keyset query over these rows instead of returning
    /// <see cref="RowsToReturn"/>.</summary>
    public List<OrderDto>? AllRows { get; set; }

    public int CallCount { get; private set; }

    public string? LastCreatorSlug { get; private set; }
    public int? LastOwnerUserId { get; private set; }
    public Guid? LastProductPublicId { get; private set; }
    public Guid? LastLandingPagePublicId { get; private set; }
    public string? LastCustomerSearch { get; private set; }
    public OrderStatus? LastStatus { get; private set; }
    public DateTimeOffset? LastAfterCreatedAt { get; private set; }
    public Guid? LastAfterId { get; private set; }
    public int? LastLimit { get; private set; }

    public Task AddAsync(Order order, CancellationToken ct) => Task.CompletedTask;

    public Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct)
        => Task.FromResult<Order?>(null);

    public Task<Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct)
        => Task.FromResult<Order?>(null);

    public Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct)
        => Task.FromResult<Order?>(null);

    public OrderListFilter? LastFilter { get; private set; }

    public Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug, int ownerUserId, OrderListFilter filter, OrderListCursor? after, int limit, CancellationToken ct)
    {
        LastCreatorSlug = creatorSlug;
        LastOwnerUserId = ownerUserId;
        LastFilter = filter;
        LastProductPublicId = filter.ProductPublicId;
        LastLandingPagePublicId = filter.LandingPagePublicId;
        LastStatus = filter.Status;
        LastCustomerSearch = filter.CustomerSearch;
        LastAfterCreatedAt = after?.At;
        LastAfterId = after?.PublicId;
        LastLimit = limit;
        CallCount++;

        if (AllRows is null)
            return Task.FromResult(RowsToReturn);

        // Keyset semantics of the real query: newest first, strictly after the cursor — by CountedAt within a period,
        // else by CreatedAt.
        var byCounted = filter.Period is not null;
        DateTimeOffset At(OrderDto o) => byCounted ? o.CountedAt : o.CreatedAt;
        return Task.FromResult(Matching(filter, includeStatus: true)
            .Where(o => after is null || At(o) < after.At || (At(o) == after.At && o.PublicId.CompareTo(after.PublicId) < 0))
            .OrderByDescending(At)
            .ThenByDescending(o => o.PublicId)
            .Take(limit)
            .ToList());
    }

    public Task<OrderStatusCountsDto> GetStatusCountsAsync(string creatorSlug, int ownerUserId, OrderListFilter filter, CancellationToken ct)
    {
        var rows = Matching(filter, includeStatus: false).ToList();
        int Of(string status) => rows.Count(o => o.Status == status);
        return Task.FromResult(new OrderStatusCountsDto(rows.Count, Of("Paid"), Of("Pending"), Of("Failed"), Of("Refunded")));
    }

    private IEnumerable<OrderDto> Matching(OrderListFilter filter, bool includeStatus) =>
        (AllRows ?? RowsToReturn).Where(o =>
            (!includeStatus || filter.Status is null || o.Status == filter.Status.ToString())
            && (filter.Period is null
                || ((filter.Period.From is null || o.CountedAt >= filter.Period.From) && o.CountedAt < filter.Period.To)));

    /// <summary>Product public ids that are not the creator's (the service answers 404).</summary>
    public HashSet<Guid> ForeignProducts { get; } = [];

    public Task<int?> GetProductIdAsync(int creatorId, Guid productPublicId, CancellationToken ct)
        => Task.FromResult<int?>(ForeignProducts.Contains(productPublicId) ? null : 1);

    public bool CreatorExists { get; set; } = true;

    public Task<string> GetCreatorTimezoneAsync(int creatorId, CancellationToken ct) => Task.FromResult("UTC");

    public Task<int?> GetCreatorIdForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult<int?>(null);

    public Task<List<TrendBucketRow>> GetRevenueTrendAsync(int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, string timezone, CancellationToken ct)
        => Task.FromResult(new List<TrendBucketRow>());

    public Task<List<TrendBucketRow>> GetViewsTrendAsync(int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, string timezone, CancellationToken ct)
        => Task.FromResult(new List<TrendBucketRow>());

    public Task<bool> CreatorExistsForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(CreatorExists);

    public Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null, 0, 0, 0, 0, 0, 0, 0, []));

    /// <summary>The workspace the list and export resolve (null when <see cref="CreatorExists"/> is false).</summary>
    public OrderPeriodContext Context { get; set; } = new(1, "UTC", "Eur", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public Task<OrderPeriodContext?> GetPeriodContextAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult<OrderPeriodContext?>(CreatorExists ? Context : null);

    public Task<OrderPeriodRead> GetPeriodSummaryAsync(int creatorId, string timezone, AnalyticsPeriodPlan plan, CancellationToken ct, int? productId = null)
        => Task.FromResult(new OrderPeriodRead(new OrderPeriodTotalsRow(0, 0, 0, 0, 0, 0, 0), []));

    public Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(int landingPageId, StatsPeriods periods, CancellationToken ct)
    {
        var none = new PeriodSalesDto(0, 0);
        return Task.FromResult(new LandingPageSalesByPeriodDto(none, none, none, none, null));
    }

    public Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new List<LandingPageOrdersSummaryDto>());

    public Task<HomeSummaryDto> GetHomeSummaryByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, [], 0, 0, 0, 0, [], [], 0, [], 0, 0, 0));
}

public sealed class FakeOrderListingHomeSummaryCache : IHomeSummaryCache
{
    public ValueTask<HomeSummaryDto?> GetAsync(int creatorId, CancellationToken ct) => ValueTask.FromResult<HomeSummaryDto?>(null);

    public Task SetAsync(int creatorId, HomeSummaryDto value, CancellationToken ct) => Task.CompletedTask;

    public Task RemoveAsync(int creatorId, CancellationToken ct) => Task.CompletedTask;
}

public sealed class FakeDashboardTrendsCache : IDashboardTrendsCache
{
    public ValueTask<DashboardTrendsDto?> GetAsync(int creatorId, string range, CancellationToken ct) =>
        ValueTask.FromResult<DashboardTrendsDto?>(null);

    public Task SetAsync(int creatorId, string range, DashboardTrendsDto value, CancellationToken ct) => Task.CompletedTask;

    public Task RemoveAsync(int creatorId, CancellationToken ct) => Task.CompletedTask;
}
