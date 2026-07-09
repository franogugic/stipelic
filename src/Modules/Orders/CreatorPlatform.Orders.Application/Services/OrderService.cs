using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IHomeSummaryCache _homeSummaryCache;

    public OrderService(IOrderRepository orderRepository, IHomeSummaryCache homeSummaryCache)
    {
        _orderRepository = orderRepository;
        _homeSummaryCache = homeSummaryCache;
    }

    public Task<List<OrderDto>> ListAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetByCreatorSlugAsync(creatorSlug, ownerUserId, ct);
    }

    public Task<OrderSummaryDto> GetSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetSummaryByCreatorSlugAsync(creatorSlug, ownerUserId, ct);
    }

    public Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
    {
        return _orderRepository.GetSummaryByLandingPageIdAsync(landingPageId, ct);
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
