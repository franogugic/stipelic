using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

/// <summary>Dedicated fake for OrderService.ListAsync tests — captures the exact args the service
/// passes down (clamped limit, cursor pass-through) and returns a canned page of rows.</summary>
public sealed class FakeOrderListingRepository : IOrderRepository
{
    public List<OrderDto> RowsToReturn { get; set; } = [];

    public string? LastCreatorSlug { get; private set; }
    public int? LastOwnerUserId { get; private set; }
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
        string creatorSlug, int ownerUserId, DateTimeOffset? afterCreatedAt, Guid? afterId, int limit, CancellationToken ct)
    {
        LastCreatorSlug = creatorSlug;
        LastOwnerUserId = ownerUserId;
        LastAfterCreatedAt = afterCreatedAt;
        LastAfterId = afterId;
        LastLimit = limit;
        return Task.FromResult(RowsToReturn);
    }

    public Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null));

    public Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null));

    public Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new List<LandingPageOrdersSummaryDto>());

    public Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
        => Task.FromResult(new List<PurchasesBucketRow>());

    public Task<HomeSummaryDto> GetHomeSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, [], 0, 0, 0, 0, [], [], 0));
}

public sealed class FakeOrderListingHomeSummaryCache : IHomeSummaryCache
{
    public bool TryGet(string creatorSlug, out HomeSummaryDto? value)
    {
        value = null;
        return false;
    }

    public void Set(string creatorSlug, HomeSummaryDto value)
    {
    }

    public void Remove(string creatorSlug)
    {
    }
}
