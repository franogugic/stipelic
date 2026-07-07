using CreatorPlatform.Orders.Application.Dtos;

namespace CreatorPlatform.Orders.Application.Interfaces;

public interface IOrderService
{
    Task<List<OrderDto>> ListAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct);

    Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct);

    Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct);

    Task<HomeSummaryDto> GetHomeSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct);
}
