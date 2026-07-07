using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Domain.Orders;

namespace CreatorPlatform.Orders.Application.Interfaces;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);

    Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct);

    Task<Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct);

    Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct);

    Task<List<OrderDto>> GetByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct);

    Task<HomeSummaryDto> GetHomeSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct);
}
