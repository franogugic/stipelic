using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;

    public OrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public Task<List<OrderDto>> ListAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetByCreatorSlugAsync(creatorSlug, ownerUserId, ct);
    }

    public Task<OrderSummaryDto> GetSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetSummaryByCreatorSlugAsync(creatorSlug, ownerUserId, ct);
    }
}
