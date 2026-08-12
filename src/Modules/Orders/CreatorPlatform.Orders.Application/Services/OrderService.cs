using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderService : IOrderService
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 100;

    private readonly IOrderRepository _orderRepository;
    private readonly IHomeSummaryCache _homeSummaryCache;

    public OrderService(IOrderRepository orderRepository, IHomeSummaryCache homeSummaryCache)
    {
        _orderRepository = orderRepository;
        _homeSummaryCache = homeSummaryCache;
    }

    public async Task<OrdersPageDto> ListAsync(
        string creatorSlug,
        int ownerUserId,
        DateTimeOffset? afterCreatedAt,
        Guid? afterId,
        int limit,
        CancellationToken ct)
    {
        var clampedLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        // Fetch one extra row to detect a next page without a separate COUNT query, then trim it.
        var rows = await _orderRepository.GetByCreatorSlugAsync(
            creatorSlug, ownerUserId, afterCreatedAt, afterId, clampedLimit + 1, ct);

        var hasMore = rows.Count > clampedLimit;
        var page = hasMore ? rows.Take(clampedLimit).ToList() : rows;

        return new OrdersPageDto(page, hasMore);
    }

    public Task<OrderSummaryDto> GetSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetSummaryByCreatorSlugAsync(creatorSlug, ownerUserId, ct);
    }

    public Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
    {
        return _orderRepository.GetSummaryByLandingPageIdAsync(landingPageId, ct);
    }

    public Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(
        string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetOrdersSummaryByCreatorGroupedByLandingPageAsync(creatorSlug, ownerUserId, ct);
    }

    public Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
    {
        return _orderRepository.GetBucketedPurchasesAsync(landingPageId, cutoff, bucketUnit, ct);
    }

    public async Task<HomeSummaryDto> GetHomeSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        if (_homeSummaryCache.TryGet(creatorSlug, out var cached) && cached is not null)
            return cached;

        var result = await _orderRepository.GetHomeSummaryByCreatorSlugAsync(creatorSlug, ownerUserId, ct);

        _homeSummaryCache.Set(creatorSlug, result);

        return result;
    }
}
