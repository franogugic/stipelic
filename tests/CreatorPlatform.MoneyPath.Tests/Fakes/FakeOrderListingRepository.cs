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

    public Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug, int ownerUserId, Guid? productPublicId, Guid? landingPagePublicId, OrderStatus? status, string? customerSearch, DateTimeOffset? afterCreatedAt, Guid? afterId, int limit, CancellationToken ct)
    {
        LastCreatorSlug = creatorSlug;
        LastOwnerUserId = ownerUserId;
        LastProductPublicId = productPublicId;
        LastLandingPagePublicId = landingPagePublicId;
        LastStatus = status;
        LastCustomerSearch = customerSearch;
        LastAfterCreatedAt = afterCreatedAt;
        LastAfterId = afterId;
        LastLimit = limit;
        CallCount++;

        if (AllRows is null)
            return Task.FromResult(RowsToReturn);

        // Keyset semantics of the real query: newest first, strictly after the (CreatedAt, PublicId) cursor.
        return Task.FromResult(AllRows
            .Where(o => afterCreatedAt is null || afterId is null
                || o.CreatedAt < afterCreatedAt
                || (o.CreatedAt == afterCreatedAt && o.PublicId.CompareTo(afterId.Value) < 0))
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.PublicId)
            .Take(limit)
            .ToList());
    }

    public bool CreatorExists { get; set; } = true;

    public Task<int?> GetCreatorIdForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult<int?>(null);

    public Task<List<TrendBucketRow>> GetRevenueTrendAsync(int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, CancellationToken ct)
        => Task.FromResult(new List<TrendBucketRow>());

    public Task<List<TrendBucketRow>> GetViewsTrendAsync(int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, CancellationToken ct)
        => Task.FromResult(new List<TrendBucketRow>());

    public Task<bool> CreatorExistsForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(CreatorExists);

    public Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null, 0, 0, 0, 0));

    public Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null, 0, 0, 0, 0));

    public Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(int landingPageId, StatsPeriods periods, CancellationToken ct)
    {
        var none = new PeriodSalesDto(0, 0);
        return Task.FromResult(new LandingPageSalesByPeriodDto(none, none, none, none, null));
    }

    public Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new List<LandingPageOrdersSummaryDto>());

    public Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
        => Task.FromResult(new List<PurchasesBucketRow>());

    public Task<HomeSummaryDto> GetHomeSummaryByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, [], 0, 0, 0, 0, [], [], 0));
}

public sealed class FakeOrderListingHomeSummaryCache : IHomeSummaryCache
{
    public bool TryGet(int creatorId, out HomeSummaryDto? value)
    {
        value = null;
        return false;
    }

    public void Set(int creatorId, HomeSummaryDto value)
    {
    }

    public void Remove(int creatorId)
    {
    }
}

public sealed class FakeDashboardTrendsCache : IDashboardTrendsCache
{
    public bool TryGet(int creatorId, string range, out DashboardTrendsDto? value)
    {
        value = null;
        return false;
    }

    public void Set(int creatorId, string range, DashboardTrendsDto value)
    {
    }
}
