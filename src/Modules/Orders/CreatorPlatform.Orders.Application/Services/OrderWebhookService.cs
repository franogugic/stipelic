using CreatorPlatform.Orders.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderWebhookService : IOrderWebhookService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrdersUnitOfWork _unitOfWork;
    private readonly ILogger<OrderWebhookService> _logger;

    public OrderWebhookService(
        IOrderRepository orderRepository,
        IOrdersUnitOfWork unitOfWork,
        ILogger<OrderWebhookService> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleCheckoutSessionCompletedAsync(OrderCheckoutCompletedDto data, CancellationToken ct)
    {
        var order = await _orderRepository.GetByStripeCheckoutSessionIdAsync(data.SessionId, ct);
        if (order is null)
        {
            _logger.LogInformation(
                "checkout.session.completed event does not match any order, ignoring. SessionId: {SessionId}",
                data.SessionId);
            return;
        }

        order.MarkPaid(data.PaymentIntentId ?? string.Empty, DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
