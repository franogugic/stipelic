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
