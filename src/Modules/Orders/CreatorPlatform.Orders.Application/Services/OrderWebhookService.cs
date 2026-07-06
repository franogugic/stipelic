using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Domain.Orders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderWebhookService : IOrderWebhookService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrdersUnitOfWork _unitOfWork;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly OrdersOptions _options;
    private readonly ILogger<OrderWebhookService> _logger;

    public OrderWebhookService(
        IOrderRepository orderRepository,
        IOrdersUnitOfWork unitOfWork,
        ICreatorContextProvider creatorContextProvider,
        IEmailOutboxService emailOutboxService,
        IOptions<OrdersOptions> options,
        ILogger<OrderWebhookService> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _creatorContextProvider = creatorContextProvider;
        _emailOutboxService = emailOutboxService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleCheckoutSessionCompletedAsync(OrderCheckoutCompletedDto data, CancellationToken ct)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var order = await _orderRepository.GetByStripeCheckoutSessionIdForUpdateAsync(data.SessionId, ct);
            if (order is null)
            {
                _logger.LogInformation(
                    "checkout.session.completed event does not match any order, ignoring. SessionId: {SessionId}",
                    data.SessionId);
                return;
            }

            if (order.Status is OrderStatus.Paid or OrderStatus.Refunded)
            {
                _logger.LogInformation(
                    "checkout.session.completed event already processed (idempotent). OrderId: {OrderId}, Status: {Status}",
                    order.PublicId,
                    order.Status);
                return;
            }

            order.MarkPaid(data.PaymentIntentId ?? string.Empty, DateTimeOffset.UtcNow);

            var productName = await _creatorContextProvider.GetProductNameAsync(order.ProductId, ct) ?? "your purchase";
            var accessUrl = $"{_options.ApiBaseUrl.TrimEnd('/')}/api/access/{order.PublicId}";

            await _emailOutboxService.QueueOrderAccessAsync(order.Email, order.PublicId.ToString(), productName, accessUrl, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task HandleChargeRefundedAsync(OrderChargeRefundedDto data, CancellationToken ct)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var order = await _orderRepository.GetByStripePaymentIntentIdAsync(data.PaymentIntentId, ct);
            if (order is null)
            {
                _logger.LogInformation(
                    "charge.refunded event does not match any order, ignoring. PaymentIntentId: {PaymentIntentId}",
                    data.PaymentIntentId);
                return;
            }

            if (order.Status == OrderStatus.Refunded)
            {
                _logger.LogInformation(
                    "charge.refunded event already processed (idempotent). OrderId: {OrderId}",
                    order.PublicId);
                return;
            }

            order.MarkRefunded(DateTimeOffset.UtcNow);

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);
    }
}
