using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Analytics;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeOrdersCreatorContextProvider : ICreatorContextProvider
{
    public LandingPageProductInfo? ProductInfo { get; set; }

    public Task<LandingPageProductInfo?> GetProductInfoByLandingPageSlugAsync(
        string creatorSlug, string landingPageSlug, CancellationToken ct)
        => Task.FromResult(ProductInfo);

    public OrderEmailContext? OrderEmailContext { get; set; } =
        new("Product", "application/zip", 18_350_080, null, "Creator", null, null, null);

    public Task<OrderEmailContext?> GetOrderEmailContextAsync(int productId, CancellationToken ct)
        => Task.FromResult(OrderEmailContext);
}

public sealed class FakePaymentCheckoutSessionService : IPaymentCheckoutSessionService
{
    public int? LastApplicationFeeAmountCents { get; private set; }
    public string? LastDestinationAccountId { get; private set; }
    public IReadOnlyDictionary<string, string>? LastMetadata { get; private set; }
    public string? LastThumbnailUrl { get; private set; }
    public string? LastSuccessUrl { get; private set; }
    public int CallCount { get; private set; }
    public string? LastCustomerEmail { get; private set; }

    public Task<PaymentCheckoutSessionDto> CreateAsync(
        string productName,
        int priceCents,
        string currency,
        string? customerEmail,
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
        LastCustomerEmail = customerEmail;
        LastApplicationFeeAmountCents = applicationFeeAmountCents;
        LastDestinationAccountId = destinationAccountId;
        LastMetadata = metadata;
        LastThumbnailUrl = thumbnailUrl;
        LastSuccessUrl = successUrl;
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
        string creatorSlug, int ownerUserId, OrderListFilter filter, OrderListCursor? after, int limit, CancellationToken ct)
        => Task.FromResult(new List<OrderDto>());

    public Task<OrderStatusCountsDto> GetStatusCountsAsync(string creatorSlug, int ownerUserId, OrderListFilter filter, CancellationToken ct)
        => Task.FromResult(new OrderStatusCountsDto(0, 0, 0, 0, 0));

    public Task<int?> GetProductIdAsync(int creatorId, Guid productPublicId, CancellationToken ct)
        => Task.FromResult<int?>(1);

    public Task<string> GetCreatorTimezoneAsync(int creatorId, CancellationToken ct) => Task.FromResult("UTC");

    public Task<int?> GetCreatorIdForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult<int?>(null);

    public Task<List<TrendBucketRow>> GetRevenueTrendAsync(int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, string timezone, CancellationToken ct)
        => Task.FromResult(new List<TrendBucketRow>());

    public Task<List<TrendBucketRow>> GetViewsTrendAsync(int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, string timezone, CancellationToken ct)
        => Task.FromResult(new List<TrendBucketRow>());

    public Task<bool> CreatorExistsForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(true);

    public Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null, 0, 0, 0, 0, 0, 0, 0, []));

    public Task<OrderPeriodContext?> GetPeriodContextAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult<OrderPeriodContext?>(null);

    public Task<OrderPeriodRead> GetPeriodSummaryAsync(int creatorId, string timezone, AnalyticsPeriodPlan plan, CancellationToken ct, int? productId = null)
        => Task.FromResult(new OrderPeriodRead(new OrderPeriodTotalsRow(0, 0, 0, 0, 0, 0, 0), []));

    public Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(int landingPageId, StatsPeriods periods, CancellationToken ct)
    {
        var none = new PeriodSalesDto(0, 0);
        return Task.FromResult(new LandingPageSalesByPeriodDto(none, none, none, none, null));
    }

    public Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new List<LandingPageOrdersSummaryDto>());

    public Task<HomeSummaryDto> GetHomeSummaryByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, [], 0, 0, 0, 0, [], [], 0, [], 0, 0, 0));
}

public sealed class FakeOrdersUnitOfWork : IOrdersUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct) => operation();
}
