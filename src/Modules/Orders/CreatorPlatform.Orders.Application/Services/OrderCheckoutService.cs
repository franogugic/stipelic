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
        string email,
        CancellationToken ct)
    {
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

        var successUrl = $"{_options.FrontendBaseUrl}/p/{creatorSlug}/{landingPageSlug}/success";
        var cancelUrl = $"{_options.FrontendBaseUrl}/p/{creatorSlug}/{landingPageSlug}";

        var metadata = new Dictionary<string, string>
        {
            ["creatorId"] = productInfo.CreatorId.ToString(),
            ["productId"] = productInfo.ProductId.ToString(),
            ["landingPageId"] = productInfo.LandingPageId.ToString(),
            ["payoutMode"] = productInfo.PayoutMode.ToString(),
            ["platformFeeCents"] = platformFeeCents.ToString(),
        };

        PaymentCheckoutSessionDto session;
        if (productInfo.PayoutMode == PayoutMode.StripeConnect)
        {
            session = await _checkoutSessionService.CreateAsync(
                productInfo.ProductName,
                productInfo.PriceCents,
                productInfo.Currency.ToString().ToLowerInvariant(),
                email,
                successUrl,
                cancelUrl,
                Guid.NewGuid().ToString(),
                metadata,
                ct,
                applicationFeeAmountCents: platformFeeCents,
                destinationAccountId: productInfo.StripeConnectAccountId);
        }
        else
        {
            session = await _checkoutSessionService.CreateAsync(
                productInfo.ProductName,
                productInfo.PriceCents,
                productInfo.Currency.ToString().ToLowerInvariant(),
                email,
                successUrl,
                cancelUrl,
                Guid.NewGuid().ToString(),
                metadata,
                ct);
        }

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
            session.SessionId,
            DateTimeOffset.UtcNow);

        await _orderRepository.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CreateCheckoutResultDto(session.CheckoutUrl);
    }
}
