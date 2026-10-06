using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Application.Templates;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Analytics;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeWebhookOrderRepository : IOrderRepository
{
    public Order? Order { get; set; }

    public Task AddAsync(Order order, CancellationToken ct)
    {
        Order = order;
        return Task.CompletedTask;
    }

    public Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct)
        => Task.FromResult(Order);

    public Task<Order?> GetByStripeCheckoutSessionIdForUpdateAsync(string stripeCheckoutSessionId, CancellationToken ct)
        => Task.FromResult(Order);

    public Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct)
        => Task.FromResult(Order);

    public Task<List<OrderDto>> GetByCreatorSlugAsync(
        string creatorSlug, int ownerUserId, Guid? productPublicId, Guid? landingPagePublicId, OrderStatus? status, string? customerSearch, DateTimeOffset? afterCreatedAt, Guid? afterId, int limit, CancellationToken ct)
        => Task.FromResult(new List<OrderDto>());

    public Task<int?> GetCreatorIdForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult<int?>(null);

    public Task<List<TrendBucketRow>> GetRevenueTrendAsync(int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, CancellationToken ct)
        => Task.FromResult(new List<TrendBucketRow>());

    public Task<List<TrendBucketRow>> GetViewsTrendAsync(int creatorId, string unit, DateTimeOffset firstBucket, DateTimeOffset lastBucket, CancellationToken ct)
        => Task.FromResult(new List<TrendBucketRow>());

    public Task<bool> CreatorExistsForOwnerAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(true);

    public Task<OrderSummaryDto> GetSummaryByCreatorSlugAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null, 0, 0, 0, 0));

    public Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
        => Task.FromResult(new OrderSummaryDto(0, 0, null, 0, 0, 0, 0));

    public Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(int landingPageId, StatsPeriods periods, CancellationToken ct)
    {
        var none = new PeriodSalesDto(0, 0);
        return Task.FromResult(new LandingPageSalesByPeriodDto(none, none, none, none, null));
    }

    public Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(new List<LandingPageOrdersSummaryDto>());

    public Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
        => Task.FromResult(new List<PurchasesBucketRow>());

    public Task<HomeSummaryDto> GetHomeSummaryByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(new HomeSummaryDto(0, 0, null, 0, 0, [], 0, null, [], 0, 0, 0, 0, [], [], 0));
}

public sealed class FakeEmailOutboxService : IEmailOutboxService
{
    public int OrderAccessQueuedCount { get; private set; }
    public int PayoutRequestedQueuedCount { get; private set; }
    public List<(string ToEmail, string Subject, string CorrelationKey, string? ReplyTo, string ListUnsubscribeUrl, string HtmlBody, string PlainTextBody)> QueuedCampaignMessages { get; } = new();
    public List<(string ToEmail, string UserPublicId, string Token)> QueuedPasswordResetMessages { get; } = new();

    public Task QueueEmailVerificationAsync(string toEmail, string? firstName, string userPublicId, string token, CancellationToken ct)
        => Task.CompletedTask;

    public Task CancelUnsentEmailVerificationMessagesAsync(string userPublicId, CancellationToken ct)
        => Task.CompletedTask;

    public Task QueuePasswordResetAsync(string toEmail, string? firstName, string userPublicId, string token, CancellationToken ct)
    {
        QueuedPasswordResetMessages.Add((toEmail, userPublicId, token));
        return Task.CompletedTask;
    }

    public List<(string ToEmail, string UserPublicId, string Token)> QueuedEmailChangeVerifications { get; } = new();
    public List<(string ToEmail, string UserPublicId, string NewEmail)> QueuedEmailChangedNotifications { get; } = new();

    public Task QueueEmailChangeVerificationAsync(
        string toEmail, string? firstName, string currentEmail, string userPublicId, string token, CancellationToken ct)
    {
        QueuedEmailChangeVerifications.Add((toEmail, userPublicId, token));
        return Task.CompletedTask;
    }

    public Task QueueEmailChangedNotificationAsync(
        string toEmail, string? firstName, string userPublicId, string newEmail, DateTimeOffset changedAt, CancellationToken ct)
    {
        QueuedEmailChangedNotifications.Add((toEmail, userPublicId, newEmail));
        return Task.CompletedTask;
    }

    public OrderAccessEmail? LastOrderAccess { get; private set; }
    public string? LastOrderAccessTo { get; private set; }
    public PayoutRequestedEmail? LastPayoutRequested { get; private set; }

    public Task QueueOrderAccessAsync(string toEmail, string orderPublicId, OrderAccessEmail order, CancellationToken ct)
    {
        OrderAccessQueuedCount++;
        LastOrderAccess = order;
        LastOrderAccessTo = toEmail;
        return Task.CompletedTask;
    }

    public Task QueuePayoutRequestedAsync(string toEmail, string payoutPublicId, PayoutRequestedEmail payout, CancellationToken ct)
    {
        PayoutRequestedQueuedCount++;
        LastPayoutRequested = payout;
        return Task.CompletedTask;
    }

    public Task QueueCampaignAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        string? replyTo,
        string listUnsubscribeUrl,
        string correlationKey,
        CancellationToken ct)
    {
        QueuedCampaignMessages.Add((toEmail, subject, correlationKey, replyTo, listUnsubscribeUrl, htmlBody, plainTextBody));
        return Task.CompletedTask;
    }
}

public sealed class FakeHomeSummaryCache : IHomeSummaryCache
{
    public bool TryGet(int creatorId, out HomeSummaryDto? value)
    {
        value = null;
        return false;
    }

    public void Set(int creatorId, HomeSummaryDto value)
    {
    }

    public void Remove(int creatorId)
    {
    }
}
