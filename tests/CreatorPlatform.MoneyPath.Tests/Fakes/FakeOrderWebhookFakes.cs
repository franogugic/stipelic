using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;

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

public sealed class FakeEmailOutboxService : IEmailOutboxService
{
    public int OrderAccessQueuedCount { get; private set; }
    public int PayoutRequestedQueuedCount { get; private set; }
    public List<(string ToEmail, string Subject, string CorrelationKey, string? ReplyTo, string ListUnsubscribeUrl)> QueuedCampaignMessages { get; } = new();

    public Task QueueEmailVerificationAsync(string toEmail, string userPublicId, string token, CancellationToken ct)
        => Task.CompletedTask;

    public Task CancelUnsentEmailVerificationMessagesAsync(string userPublicId, CancellationToken ct)
        => Task.CompletedTask;

    public Task QueueOrderAccessAsync(string toEmail, string orderPublicId, string productName, string accessUrl, CancellationToken ct)
    {
        OrderAccessQueuedCount++;
        return Task.CompletedTask;
    }

    public Task QueuePayoutRequestedAsync(
        string toEmail,
        string payoutPublicId,
        string creatorName,
        string creatorSlug,
        int amountCents,
        string currency,
        CancellationToken ct)
    {
        PayoutRequestedQueuedCount++;
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
        QueuedCampaignMessages.Add((toEmail, subject, correlationKey, replyTo, listUnsubscribeUrl));
        return Task.CompletedTask;
    }
}

public sealed class FakeHomeSummaryCache : IHomeSummaryCache
{
    public bool TryGet(string creatorSlug, out HomeSummaryDto? value)
    {
        value = null;
        return false;
    }

    public void Set(string creatorSlug, HomeSummaryDto value)
    {
    }

    public void Remove(string creatorSlug)
    {
    }
}
