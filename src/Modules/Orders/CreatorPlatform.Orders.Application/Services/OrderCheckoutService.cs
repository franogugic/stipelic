using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderCheckoutService : IOrderCheckoutService
{
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IPaymentCheckoutSessionService _checkoutSessionService;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrdersUnitOfWork _unitOfWork;
    private readonly OrdersOptions _options;

    public OrderCheckoutService(
        ICreatorContextProvider creatorContextProvider,
        IPaymentCheckoutSessionService checkoutSessionService,
        IOrderRepository orderRepository,
        IOrdersUnitOfWork unitOfWork,
        IOptions<OrdersOptions> options)
    {
        _creatorContextProvider = creatorContextProvider;
        _checkoutSessionService = checkoutSessionService;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
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

        var successUrl = $"{_options.FrontendBaseUrl}/p/{creatorSlug}/{landingPageSlug}/success";
        var cancelUrl = $"{_options.FrontendBaseUrl}/p/{creatorSlug}/{landingPageSlug}";

        var metadata = new Dictionary<string, string>
        {
            ["creatorId"] = productInfo.CreatorId.ToString(),
            ["productId"] = productInfo.ProductId.ToString(),
            ["landingPageId"] = productInfo.LandingPageId.ToString(),
        };

        var session = await _checkoutSessionService.CreateAsync(
            productInfo.ProductName,
            productInfo.PriceCents,
            productInfo.Currency,
            email,
            successUrl,
            cancelUrl,
            Guid.NewGuid().ToString(),
            metadata,
            ct);

        var order = Order.Create(
            productInfo.CreatorId,
            productInfo.ProductId,
            productInfo.LandingPageId,
            email,
            name: null,
            productInfo.PriceCents,
            productInfo.Currency,
            session.SessionId,
            DateTimeOffset.UtcNow);

        await _orderRepository.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CreateCheckoutResultDto(session.CheckoutUrl);
    }
}
