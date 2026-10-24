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
        => Task.FromResult(Order?.StripeCheckoutSessionId == stripeCheckoutSessionId ? Order : null);

    public Task<Order?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct)
        => Task.FromResult(Order?.PublicId == publicId ? Order : null);

    public Task<Order?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct)
        => Task.FromResult(Order);

    public Task<Order?> GetByStripePaymentIntentIdForUpdateAsync(string stripePaymentIntentId, CancellationToken ct)
        => Task.FromResult(Order);

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

    public int EmailsSentThisMonth { get; set; }

    public Task<int> GetEmailsSentThisMonthAsync(int creatorId, CancellationToken ct) => Task.FromResult(EmailsSentThisMonth);
}

public sealed class FakeEmailOutboxService : IEmailOutboxService
{
    public int OrderAccessQueuedCount { get; private set; }
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

    public Task QueueOrderAccessAsync(string toEmail, string orderPublicId, OrderAccessEmail order, CancellationToken ct)
    {
        OrderAccessQueuedCount++;
        LastOrderAccess = order;
        LastOrderAccessTo = toEmail;
        return Task.CompletedTask;
    }

    public List<(string ToEmail, string CorrelationKey, LeadMagnetEmail Lead)> QueuedLeadMagnets { get; } = new();

    public Task QueueLeadMagnetAsync(string toEmail, string correlationKey, LeadMagnetEmail lead, CancellationToken ct)
    {
        QueuedLeadMagnets.Add((toEmail, correlationKey, lead));
        return Task.CompletedTask;
    }

    public Task QueueCampaignAsync(
        int creatorId,
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
    public ValueTask<HomeSummaryDto?> GetAsync(int creatorId, CancellationToken ct) => ValueTask.FromResult<HomeSummaryDto?>(null);

    public Task SetAsync(int creatorId, HomeSummaryDto value, CancellationToken ct) => Task.CompletedTask;

    public Task RemoveAsync(int creatorId, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>The buyers' personal links in memory: one per order, like the unique index.</summary>
public sealed class FakeOrderAccessGrants : IOrderAccessGrants
{
    public Dictionary<int, (string Token, string Email, DateTimeOffset? RevokedAt)> ByOrderId { get; } = [];
    public int RevokeCallCount { get; private set; }

    public string CreateForPaidOrder(int creatorId, int productId, int orderId, string buyerEmail, DateTimeOffset createdAt)
    {
        if (ByOrderId.ContainsKey(orderId))
            throw new InvalidOperationException($"Order {orderId} already has a grant (unique index violation).");

        var token = CreatorPlatform.Products.Domain.Access.AccessGrant.NewToken();
        ByOrderId[orderId] = (token, buyerEmail, null);
        return token;
    }

    public Task RevokeForOrderAsync(int orderId, DateTimeOffset revokedAt, CancellationToken ct)
    {
        RevokeCallCount++;
        if (ByOrderId.TryGetValue(orderId, out var grant) && grant.RevokedAt is null)
            ByOrderId[orderId] = grant with { RevokedAt = revokedAt };
        return Task.CompletedTask;
    }
}
