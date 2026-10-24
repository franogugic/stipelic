using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;
using CreatorPlatform.Shared.Domain.Money;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderCheckoutService : IOrderCheckoutService
{
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IPaymentCheckoutSessionService _checkoutSessionService;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrdersUnitOfWork _unitOfWork;
    private readonly OrdersOptions _options;
    private readonly ILogger<OrderCheckoutService> _logger;

    public OrderCheckoutService(
        ICreatorContextProvider creatorContextProvider,
        IPaymentCheckoutSessionService checkoutSessionService,
        IOrderRepository orderRepository,
        IOrdersUnitOfWork unitOfWork,
        IOptions<OrdersOptions> options,
        ILogger<OrderCheckoutService> logger)
    {
        _creatorContextProvider = creatorContextProvider;
        _checkoutSessionService = checkoutSessionService;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CreateCheckoutResultDto> CreateCheckoutAsync(
        string creatorSlug,
        string landingPageSlug,
        string? buyerEmail,
        CancellationToken ct)
    {
        var email = string.IsNullOrWhiteSpace(buyerEmail) ? null : buyerEmail.Trim();

        var productInfo = await _creatorContextProvider.GetProductInfoByLandingPageSlugAsync(creatorSlug, landingPageSlug, ct);
        if (productInfo is null)
            throw new NotFoundException("Landing page not found.");

        if (productInfo.CreatorStatus != CreatorStatus.Active)
            throw new BadRequestException("Creator is not active.");

        if (productInfo.PlatformFeeBasisPoints is null)
        {
            _logger.LogWarning(
                "Checkout blocked: creator {CreatorId} has no active subscription.",
                productInfo.CreatorId);
            throw new ConflictException("Creator has no active subscription.");
        }

        if (productInfo.PriceCents <= 0)
            throw new BadRequestException("Product price must be greater than zero.");

        var payoutReady = productInfo.PayoutMode == PayoutMode.StripeConnect
            ? productInfo.StripeConnectPayoutsEnabled
            : productInfo.HasPayoutProfile;

        if (!payoutReady)
            throw new ConflictException("Creator has not completed payout setup.");

        var platformFeeBasisPoints = productInfo.PlatformFeeBasisPoints.Value;
        var platformFeeCents = PlatformFee.Calculate(productInfo.PriceCents, platformFeeBasisPoints);

        // {CHECKOUT_SESSION_ID} is Stripe's literal placeholder (filled in on redirect) — it must stay unencoded. The
        // success page reads the receipt by it.
        var successUrl = $"{_options.FrontendBaseUrl}/p/{creatorSlug}/{landingPageSlug}/success?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{_options.FrontendBaseUrl}/p/{creatorSlug}/{landingPageSlug}";

        // The Pending order is committed first: whatever happens after the Stripe call, a payable session always has
        // its order (B2). The session carries the order's PublicId, and that PublicId is the Stripe idempotency key,
        // so a retried create returns the same session instead of a second payable one.
        var order = Order.Create(
            productInfo.CreatorId,
            productInfo.ProductId,
            productInfo.LandingPageId,
            email,
            name: null,
            productInfo.PriceCents,
            productInfo.Currency,
            platformFeeBasisPoints,
            platformFeeCents,
            payoutMode: productInfo.PayoutMode.ToString(),
            stripeCheckoutSessionId: null,
            DateTimeOffset.UtcNow);

        await _orderRepository.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var metadata = new Dictionary<string, string>
        {
            [CheckoutSessionMetadata.OrderId] = order.PublicId.ToString(),
            ["creatorId"] = productInfo.CreatorId.ToString(),
            [CheckoutSessionMetadata.ProductId] = productInfo.ProductId.ToString(),
            ["landingPageId"] = productInfo.LandingPageId.ToString(),
            ["payoutMode"] = productInfo.PayoutMode.ToString(),
            ["platformFeeCents"] = platformFeeCents.ToString(),
        };

        var isConnect = productInfo.PayoutMode == PayoutMode.StripeConnect;
        PaymentCheckoutSessionDto session;
        try
        {
            session = await _checkoutSessionService.CreateAsync(
                productInfo.ProductName,
                productInfo.PriceCents,
                productInfo.Currency.ToString().ToLowerInvariant(),
                email,
                successUrl,
                cancelUrl,
                idempotencyKey: order.PublicId.ToString(),
                metadata,
                ct,
                applicationFeeAmountCents: isConnect ? platformFeeCents : null,
                destinationAccountId: isConnect ? productInfo.StripeConnectAccountId : null,
                thumbnailUrl: productInfo.ThumbnailUrl);
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(order, exception);
            throw;
        }

        try
        {
            order.AttachCheckoutSession(session.SessionId, DateTimeOffset.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The buyer can still pay: checkout.session.completed finds the order through the session's metadata and
            // stores the session id then. Failing here would only strand a payable session.
            _logger.LogWarning(
                exception,
                "Could not store the Checkout session on its order; the webhook will link it from the metadata. OrderId: {OrderId}, SessionId: {SessionId}",
                order.PublicId,
                session.SessionId);
        }

        return new CreateCheckoutResultDto(session.CheckoutUrl);
    }

    /// <summary>The Stripe call failed: no session can be paid for this order, so it is Failed rather than Pending
    /// forever. Best effort — if this save fails too, the maintenance job fails it once it is old.</summary>
    private async Task MarkFailedAsync(Order order, Exception cause)
    {
        _logger.LogWarning(
            cause,
            "Creating the Checkout session failed; marking the order Failed. OrderId: {OrderId}",
            order.PublicId);

        try
        {
            order.MarkFailed(DateTimeOffset.UtcNow);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not mark the order Failed after the Checkout session failed; it stays Pending. OrderId: {OrderId}",
                order.PublicId);
        }
    }
}
