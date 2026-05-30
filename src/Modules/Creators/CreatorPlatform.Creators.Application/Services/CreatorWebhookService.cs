using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payments.Application.Dtos;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Creators.Application.Services;

public sealed class CreatorWebhookService : ICreatorWebhookService
{
    private readonly ICreatorRepository _creatorRepository;
    private readonly ICreatorSubscriptionRepository _creatorSubscriptionRepository;
    private readonly ICreatorsUnitOfWork _unitOfWork;
    private readonly ILogger<CreatorWebhookService> _logger;

    public CreatorWebhookService(
        ICreatorRepository creatorRepository,
        ICreatorSubscriptionRepository creatorSubscriptionRepository,
        ICreatorsUnitOfWork unitOfWork,
        ILogger<CreatorWebhookService> logger)
    {
        _creatorRepository = creatorRepository;
        _creatorSubscriptionRepository = creatorSubscriptionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleCheckoutSessionCompletedAsync(
        CheckoutSessionCompletedData data,
        CancellationToken ct)
    {
        if (!data.Metadata.TryGetValue("creatorId", out var creatorIdStr)
            || !int.TryParse(creatorIdStr, out var creatorId))
        {
            _logger.LogWarning(
                "checkout.session.completed event missing or invalid creatorId in metadata. SessionId: {SessionId}",
                data.SessionId);
            return;
        }

        if (!data.Metadata.TryGetValue("subscriptionId", out var subscriptionIdStr)
            || !int.TryParse(subscriptionIdStr, out var subscriptionId))
        {
            _logger.LogWarning(
                "checkout.session.completed event missing or invalid subscriptionId in metadata. SessionId: {SessionId}",
                data.SessionId);
            return;
        }

        if (string.IsNullOrWhiteSpace(data.StripeSubscriptionId))
        {
            _logger.LogWarning(
                "checkout.session.completed event has no StripeSubscriptionId. SessionId: {SessionId}",
                data.SessionId);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var creator = await _creatorRepository.GetByIdForUpdateAsync(creatorId, ct);
            if (creator is null)
            {
                _logger.LogWarning(
                    "Creator not found for webhook activation. CreatorId: {CreatorId}, SessionId: {SessionId}",
                    creatorId, data.SessionId);
                return;
            }

            var subscription = await _creatorSubscriptionRepository.GetByIdForUpdateAsync(subscriptionId, ct);
            if (subscription is null)
            {
                _logger.LogWarning(
                    "Subscription not found for webhook activation. SubscriptionId: {SubscriptionId}, SessionId: {SessionId}",
                    subscriptionId, data.SessionId);
                return;
            }

            // Idempotency: already processed
            if (creator.Status == CreatorStatus.Active && subscription.Status == CreatorSubscriptionStatus.Active)
            {
                _logger.LogInformation(
                    "Creator and subscription already active, skipping activation. CreatorId: {CreatorId}, SessionId: {SessionId}",
                    creatorId, data.SessionId);
                return;
            }

            if (creator.Status != CreatorStatus.PendingPayment && creator.Status != CreatorStatus.Active)
            {
                _logger.LogWarning(
                    "Unexpected creator status for checkout activation. CreatorId: {CreatorId}, Status: {Status}, SessionId: {SessionId}",
                    creatorId, creator.Status, data.SessionId);
                return;
            }

            if (subscription.Status != CreatorSubscriptionStatus.PendingPayment
                && subscription.Status != CreatorSubscriptionStatus.Active)
            {
                _logger.LogWarning(
                    "Unexpected subscription status for checkout activation. SubscriptionId: {SubscriptionId}, Status: {Status}, SessionId: {SessionId}",
                    subscriptionId, subscription.Status, data.SessionId);
                return;
            }

            subscription.ActivateWithProvider(
                data.StripeSubscriptionId,
                data.CurrentPeriodStart,
                data.CurrentPeriodEnd,
                now);

            if (!string.IsNullOrWhiteSpace(data.StripeCustomerId))
                creator.SetStripeCustomerId(data.StripeCustomerId, now);

            creator.Activate(now);

            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Creator activated via Stripe checkout. CreatorId: {CreatorId}, SubscriptionId: {SubscriptionId}, StripeSubscriptionId: {StripeSubscriptionId}",
                creatorId, subscriptionId, data.StripeSubscriptionId);
        }, ct);
    }

    public async Task HandleSubscriptionUpdatedAsync(
        SubscriptionChangedData data,
        CancellationToken ct)
    {
        var subscription = await _creatorSubscriptionRepository
            .GetByProviderSubscriptionIdForUpdateAsync(data.StripeSubscriptionId, ct);

        if (subscription is null)
        {
            _logger.LogInformation(
                "No subscription found for customer.subscription.updated. StripeSubscriptionId: {StripeSubscriptionId} — likely a non-platform subscription, ignoring.",
                data.StripeSubscriptionId);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            switch (data.Status)
            {
                case "active":
                    subscription.UpdatePeriod(data.CurrentPeriodStart, data.CurrentPeriodEnd, now);

                    if (subscription.Creator.Status != CreatorStatus.Active)
                        subscription.Creator.Activate(now);

                    _logger.LogInformation(
                        "Subscription period updated via Stripe. StripeSubscriptionId: {StripeSubscriptionId}",
                        data.StripeSubscriptionId);
                    break;

                case "past_due":
                    subscription.MarkPastDue(now);

                    _logger.LogWarning(
                        "Subscription marked past due. StripeSubscriptionId: {StripeSubscriptionId}",
                        data.StripeSubscriptionId);
                    break;

                case "canceled":
                case "cancelled":
                    subscription.Cancel(now);
                    subscription.Creator.Suspend(now);

                    _logger.LogWarning(
                        "Subscription cancelled via subscription.updated event. StripeSubscriptionId: {StripeSubscriptionId}",
                        data.StripeSubscriptionId);
                    break;

                default:
                    _logger.LogInformation(
                        "Unhandled subscription status in customer.subscription.updated. Status: {Status}, StripeSubscriptionId: {StripeSubscriptionId}",
                        data.Status, data.StripeSubscriptionId);
                    return;
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task HandleSubscriptionDeletedAsync(
        SubscriptionChangedData data,
        CancellationToken ct)
    {
        var subscription = await _creatorSubscriptionRepository
            .GetByProviderSubscriptionIdForUpdateAsync(data.StripeSubscriptionId, ct);

        if (subscription is null)
        {
            _logger.LogInformation(
                "No subscription found for customer.subscription.deleted. StripeSubscriptionId: {StripeSubscriptionId} — likely a non-platform subscription, ignoring.",
                data.StripeSubscriptionId);
            return;
        }

        // Idempotency: already cancelled
        if (subscription.Status == CreatorSubscriptionStatus.Cancelled)
        {
            _logger.LogInformation(
                "Subscription already cancelled, skipping. StripeSubscriptionId: {StripeSubscriptionId}",
                data.StripeSubscriptionId);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            subscription.Cancel(now);
            subscription.Creator.Suspend(now);

            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Creator suspended due to subscription deletion. CreatorId: {CreatorId}, StripeSubscriptionId: {StripeSubscriptionId}",
                subscription.Creator.Id, data.StripeSubscriptionId);
        }, ct);
    }

    public async Task HandleInvoicePaymentFailedAsync(
        InvoicePaymentFailedData data,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(data.StripeSubscriptionId))
        {
            _logger.LogInformation(
                "invoice.payment_failed event has no StripeSubscriptionId — likely a one-off invoice, ignoring.");
            return;
        }

        var subscription = await _creatorSubscriptionRepository
            .GetByProviderSubscriptionIdForUpdateAsync(data.StripeSubscriptionId, ct);

        if (subscription is null)
        {
            _logger.LogInformation(
                "No subscription found for invoice.payment_failed. StripeSubscriptionId: {StripeSubscriptionId} — likely a non-platform subscription, ignoring.",
                data.StripeSubscriptionId);
            return;
        }

        // Already past due or cancelled — nothing to do
        if (subscription.Status is CreatorSubscriptionStatus.PastDue or CreatorSubscriptionStatus.Cancelled)
        {
            _logger.LogInformation(
                "Subscription already in {Status} state, skipping payment failed handler. StripeSubscriptionId: {StripeSubscriptionId}",
                subscription.Status, data.StripeSubscriptionId);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            subscription.MarkPastDue(now);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Subscription marked past due due to failed invoice payment. StripeSubscriptionId: {StripeSubscriptionId}",
                data.StripeSubscriptionId);
        }, ct);
    }
}
