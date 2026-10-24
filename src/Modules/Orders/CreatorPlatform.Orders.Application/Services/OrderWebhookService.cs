using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Application.Templates;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Application.Receipts;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Payouts.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderWebhookService : IOrderWebhookService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrdersUnitOfWork _unitOfWork;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly IHomeSummaryCache _homeSummaryCache;
    private readonly IPayoutLedgerService _payoutLedgerService;
    private readonly IOrderAccessGrants _accessGrants;
    private readonly OrdersOptions _options;
    private readonly ILogger<OrderWebhookService> _logger;

    public OrderWebhookService(
        IOrderRepository orderRepository,
        IOrdersUnitOfWork unitOfWork,
        ICreatorContextProvider creatorContextProvider,
        IEmailOutboxService emailOutboxService,
        IHomeSummaryCache homeSummaryCache,
        IPayoutLedgerService payoutLedgerService,
        IOrderAccessGrants accessGrants,
        IOptions<OrdersOptions> options,
        ILogger<OrderWebhookService> logger)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _creatorContextProvider = creatorContextProvider;
        _emailOutboxService = emailOutboxService;
        _homeSummaryCache = homeSummaryCache;
        _payoutLedgerService = payoutLedgerService;
        _accessGrants = accessGrants;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleCheckoutSessionCompletedAsync(OrderCheckoutCompletedDto data, CancellationToken ct)
    {
        int? affectedCreatorId = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var order = await FindSaleOrderForUpdateAsync(data.SessionId, data.Metadata, "checkout.session.completed", ct);
            if (order is null)
                return;

            if (order.Status is OrderStatus.Paid or OrderStatus.Refunded)
            {
                _logger.LogInformation(
                    "checkout.session.completed event already processed (idempotent). OrderId: {OrderId}, Status: {Status}",
                    order.PublicId,
                    order.Status);
                return;
            }

            var paidAt = DateTimeOffset.UtcNow;
            if (order.Email is null && string.IsNullOrWhiteSpace(data.CustomerEmail))
            {
                // Stripe always collects an email, so this should never happen. Throwing rolls the transaction
                // back (the order stays Pending) and the controller records a webhook failure for reprocessing.
                _logger.LogError(
                    "checkout.session.completed has no customer email for an order without one. OrderId: {OrderId}, SessionId: {SessionId}",
                    order.PublicId,
                    data.SessionId);
                throw new InvalidOperationException(
                    $"checkout.session.completed for order {order.PublicId} carries no customer email.");
            }

            order.MarkPaid(data.PaymentIntentId ?? string.Empty, paidAt, data.CustomerEmail, data.CustomerName);
            var buyerEmail = order.Email!;

            // Connect orders never touch the ledger — the money already left via the destination-charge
            // split, so there is no platform-held balance to track for them.
            if (order.PayoutMode == nameof(PayoutMode.BankTransfer))
            {
                await _payoutLedgerService.AppendSaleAsync(
                    order.CreatorId, order.Id, order.AmountCents, order.PlatformFeeCents, order.Currency, paidAt, ct);
            }

            // The buyer's personal link, created with the payment: no paid order without one.
            var token = _accessGrants.CreateForPaidOrder(order.CreatorId, order.ProductId, order.Id, buyerEmail, paidAt);
            var downloadUrl = $"{_options.FrontendBaseUrl.TrimEnd('/')}/d/{token}";
            var emailContext = await _creatorContextProvider.GetOrderEmailContextAsync(order.ProductId, ct);

            await _emailOutboxService.QueueOrderAccessAsync(
                buyerEmail,
                order.PublicId.ToString(),
                new OrderAccessEmail(
                    OrderNumbers.From(order.PublicId),
                    order.Name,
                    buyerEmail,
                    emailContext?.ProductName ?? "your purchase",
                    EmailFileLabel.From(emailContext?.FileContentType, emailContext?.FileSizeBytes),
                    emailContext?.ProductThumbnailUrl,
                    order.AmountCents,
                    order.Currency.ToString(),
                    paidAt,
                    downloadUrl,
                    emailContext?.CreatorName ?? "Luma",
                    emailContext?.BrandColor,
                    emailContext?.LogoUrl,
                    emailContext?.SupportEmail),
                ct);

            await _unitOfWork.SaveChangesAsync(ct);

            affectedCreatorId = order.CreatorId;
        }, ct);

        // Revenue / order count / recent transactions on the creator's home summary changed — drop the cache
        // so the dashboard recomputes on next read instead of serving up to 5 min stale numbers.
        await InvalidateHomeSummaryAsync(affectedCreatorId);
    }

    public async Task HandleCheckoutSessionExpiredAsync(OrderCheckoutExpiredDto data, CancellationToken ct)
    {
        int? affectedCreatorId = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var order = await FindSaleOrderForUpdateAsync(
                data.SessionId, data.Metadata, "checkout.session.expired", ct, orderRequired: false);
            if (order is null)
                return;

            if (order.Status != OrderStatus.Pending)
            {
                _logger.LogInformation(
                    "checkout.session.expired for an order that is not Pending, ignoring. OrderId: {OrderId}, Status: {Status}",
                    order.PublicId, order.Status);
                return;
            }

            order.MarkFailed(DateTimeOffset.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
            affectedCreatorId = order.CreatorId;

            _logger.LogInformation(
                "Checkout session expired; order marked Failed. OrderId: {OrderId}, SessionId: {SessionId}",
                order.PublicId, data.SessionId);
        }, ct);

        // The pending count on the creator's home summary dropped.
        await InvalidateHomeSummaryAsync(affectedCreatorId);
    }

    public async Task HandleChargeRefundedAsync(OrderChargeRefundedDto data, CancellationToken ct)
    {
        int? affectedCreatorId = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var order = await _orderRepository.GetByStripePaymentIntentIdForUpdateAsync(data.PaymentIntentId, ct);
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

            var refundedTo = RefundedToOrderCents(order, data);
            var refundedAt = DateTimeOffset.UtcNow;
            if (!order.ApplyRefund(refundedTo, refundedAt))
            {
                _logger.LogInformation(
                    "charge.refunded adds nothing to the order's refunds (replayed or out of order), ignoring. OrderId: {OrderId}, Status: {Status}, RefundedCents: {RefundedCents}, EventRefundedTo: {EventRefundedTo}",
                    order.PublicId, order.Status, order.RefundedCents, refundedTo);
                return;
            }

            // Only a full refund takes the buyer's link away; a partly refunded buyer keeps the file.
            if (order.Status == OrderStatus.Refunded)
                await _accessGrants.RevokeForOrderAsync(order.Id, refundedAt, ct);

            if (order.PayoutMode == nameof(PayoutMode.BankTransfer))
            {
                await _payoutLedgerService.AppendRefundAsync(
                    order.CreatorId, order.Id, order.AmountCents, order.PlatformFeeCents, order.RefundedCents, order.Currency,
                    refundedAt, ct);
            }

            _logger.LogInformation(
                "Order refund applied. OrderId: {OrderId}, Status: {Status}, RefundedCents: {RefundedCents} of {AmountCents}",
                order.PublicId, order.Status, order.RefundedCents, order.AmountCents);

            await _unitOfWork.SaveChangesAsync(ct);

            affectedCreatorId = order.CreatorId;
        }, ct);

        // Refund lowers revenue on the creator's home summary — invalidate so it isn't stale.
        await InvalidateHomeSummaryAsync(affectedCreatorId);
    }

    /// <summary>
    /// The order a sale session belongs to, row locked: by the stored session id, else by the order's PublicId in the
    /// session metadata (the order is saved before its session, and storing the session id on it may have failed) —
    /// the session id is then stored. Null for a session that is not a product sale (a plan subscription's).
    /// A sale session whose order is missing, or an order already tied to another session, throws: the event is
    /// recorded as a webhook failure for an admin instead of being ignored — unless <paramref name="orderRequired"/> is
    /// false (an expired session: nothing was paid, so a missing order only needs a warning).
    /// </summary>
    private async Task<Order?> FindSaleOrderForUpdateAsync(
        string sessionId, IReadOnlyDictionary<string, string>? metadata, string eventType, CancellationToken ct,
        bool orderRequired = true)
    {
        var order = await _orderRepository.GetByStripeCheckoutSessionIdForUpdateAsync(sessionId, ct);
        if (order is not null)
            return order;

        string? orderIdValue = null;
        var hasOrderId = metadata is not null && metadata.TryGetValue(CheckoutSessionMetadata.OrderId, out orderIdValue);
        var isSale = hasOrderId || (metadata?.ContainsKey(CheckoutSessionMetadata.ProductId) ?? false);
        if (!isSale)
        {
            _logger.LogInformation(
                "{EventType} is not for a product sale, ignoring. SessionId: {SessionId}", eventType, sessionId);
            return null;
        }

        if (hasOrderId && Guid.TryParse(orderIdValue, out var orderPublicId))
            order = await _orderRepository.GetByPublicIdForUpdateAsync(orderPublicId, ct);

        if (order is null && !orderRequired)
        {
            _logger.LogWarning(
                "{EventType} for a product sale matches no order, ignoring. SessionId: {SessionId}, OrderId: {OrderId}",
                eventType, sessionId, orderIdValue);
            return null;
        }

        if (order is null)
        {
            _logger.LogError(
                "{EventType} for a product sale matches no order. SessionId: {SessionId}, OrderId: {OrderId}",
                eventType, sessionId, orderIdValue);
            throw new InvalidOperationException(
                $"{eventType} for sale session {sessionId} matches no order (metadata orderId '{orderIdValue}').");
        }

        // Throws when the order already belongs to another session.
        order.AttachCheckoutSession(sessionId, DateTimeOffset.UtcNow);
        _logger.LogInformation(
            "Checkout session linked to its order from the session metadata. OrderId: {OrderId}, SessionId: {SessionId}",
            order.PublicId, sessionId);
        return order;
    }

    /// <summary>The charge's cumulative refund in the order's currency. The charge may be in the buyer's currency
    /// (Adaptive Pricing), so a partial refund is taken as its share of the charge and applied to the order amount;
    /// it stays between 1 cent and one cent under the full amount, so only a full refund marks the order Refunded.</summary>
    private static int RefundedToOrderCents(Order order, OrderChargeRefundedDto data)
    {
        if (data.FullyRefunded || data.ChargeAmountRefundedCents >= data.ChargeAmountCents || order.AmountCents <= 1)
            return order.AmountCents;

        if (data.ChargeAmountCents <= 0 || data.ChargeAmountRefundedCents <= 0)
            throw new InvalidOperationException(
                $"charge.refunded for order {order.PublicId} carries no usable amounts (amount {data.ChargeAmountCents}, refunded {data.ChargeAmountRefundedCents}).");

        var share = (decimal)order.AmountCents * data.ChargeAmountRefundedCents / data.ChargeAmountCents;
        return (int)Math.Clamp(Math.Round(share, MidpointRounding.AwayFromZero), 1, order.AmountCents - 1);
    }

    // CancellationToken.None: the change is already committed, so an aborted request must not leave a stale summary.
    private async Task InvalidateHomeSummaryAsync(int? creatorId)
    {
        if (creatorId is int id)
            await _homeSummaryCache.RemoveAsync(id, CancellationToken.None);
    }
}
