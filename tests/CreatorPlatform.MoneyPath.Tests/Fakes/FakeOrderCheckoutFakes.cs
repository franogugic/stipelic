using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeOrdersCreatorContextProvider : ICreatorContextProvider
{
    public LandingPageProductInfo? ProductInfo { get; set; }

    public Task<LandingPageProductInfo?> GetProductInfoByLandingPageSlugAsync(
        string creatorSlug, string landingPageSlug, CancellationToken ct)
        => Task.FromResult(ProductInfo);

    public Task<string?> GetProductNameAsync(int productId, CancellationToken ct)
        => Task.FromResult<string?>("Product");

    public Task<string?> GetCreatorSlugByIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult<string?>("creator-slug");
}

public sealed class FakePaymentCheckoutSessionService : IPaymentCheckoutSessionService
{
    public int? LastApplicationFeeAmountCents { get; private set; }
    public string? LastDestinationAccountId { get; private set; }
    public IReadOnlyDictionary<string, string>? LastMetadata { get; private set; }
    public string? LastThumbnailUrl { get; private set; }
    public int CallCount { get; private set; }

    public Task<PaymentCheckoutSessionDto> CreateAsync(
        string productName,
        int priceCents,
        string currency,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct,
        int? applicationFeeAmountCents = null,
        string? destinationAccountId = null,
        string? thumbnailUrl = null)
    {
        CallCount++;
        LastApplicationFeeAmountCents = applicationFeeAmountCents;
        LastDestinationAccountId = destinationAccountId;
        LastMetadata = metadata;
        LastThumbnailUrl = thumbnailUrl;
        return Task.FromResult(new PaymentCheckoutSessionDto("sess_123", "https://checkout.stripe.com/sess_123"));
    }
}

public sealed class FakeOrderRepository : IOrderRepository
{
    public Order? AddedOrder { get; private set; }

    public Task AddAsync(Order order, CancellationToken ct)
    {
        AddedOrder = order;
        return Task.CompletedTask;
    }

    public Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct)
        => Task.FromResult<Order?>(null);

    public Task<Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct)
        => Task.FromResult<Order?>(null);

    public Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct)
        => Task.FromResult<Order?>(null);

    public Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug, int ownerUserId, DateTimeOffset? afterCreatedAt, Guid? afterId, int limit, CancellationToken ct)
        => Task.FromResult(new List<OrderDto>());

    public Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null));

    public Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null));

    public Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
        => Task.FromResult(new List<PurchasesBucketRow>());

    public Task<HomeSummaryDto> GetHomeSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, [], 0, 0));
}

public sealed class FakeOrdersUnitOfWork : IOrdersUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct) => operation();
}
