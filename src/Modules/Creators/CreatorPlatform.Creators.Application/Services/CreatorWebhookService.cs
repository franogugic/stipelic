using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Domain;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Creators.Application.Services;

public sealed class CreatorWebhookService : ICreatorWebhookService
{
    private readonly ICreatorRepository _creatorRepository;
    private readonly ICreatorSubscriptionRepository _creatorSubscriptionRepository;
    private readonly ICreatorPlanRepository _creatorPlanRepository;
    private readonly IWebhookFailureRepository _webhookFailureRepository;
    private readonly ICreatorsUnitOfWork _unitOfWork;
    private readonly ISubscriptionBillingPeriodService _billingPeriodService;
    private readonly ICreatorCacheInvalidator _cacheInvalidator;
    private readonly ILogger<CreatorWebhookService> _logger;

    private const string FreePlanCode = "free";

    public CreatorWebhookService(
        ICreatorRepository creatorRepository,
        ICreatorSubscriptionRepository creatorSubscriptionRepository,
        ICreatorPlanRepository creatorPlanRepository,
        IWebhookFailureRepository webhookFailureRepository,
        ICreatorsUnitOfWork unitOfWork,
        ISubscriptionBillingPeriodService billingPeriodService,
        ICreatorCacheInvalidator cacheInvalidator,
        ILogger<CreatorWebhookService> logger)
    {
        _creatorRepository = creatorRepository;
        _creatorSubscriptionRepository = creatorSubscriptionRepository;
        _creatorPlanRepository = creatorPlanRepository;
        _webhookFailureRepository = webhookFailureRepository;
        _unitOfWork = unitOfWork;
        _billingPeriodService = billingPeriodService;
        _cacheInvalidator = cacheInvalidator;
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

        // Read outside the transaction (no HTTP call while holding row locks). Never blocks activation: on failure
        // the period stays as it is and customer.subscription.created/updated fills it in.
        var period = await TryReadBillingPeriodAsync(data.StripeSubscriptionId, data.SessionId, ct);

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

            // The workspace switched to the Free plan (ContinueOnFreePlanAsync) but the customer still paid —
            // normally impossible because the session is expired first, but a legacy pending subscription has no
            // stored session to expire. Never activate it: record a webhook failure so an admin can refund the
            // charge and cancel the Stripe subscription.
            if (subscription.Status == CreatorSubscriptionStatus.Cancelled)
            {
                _logger.LogWarning(
                    "Checkout completed for a cancelled pending subscription; not activating. SubscriptionId: {SubscriptionId}, StripeSubscriptionId: {StripeSubscriptionId}, SessionId: {SessionId}",
                    subscriptionId, data.StripeSubscriptionId, data.SessionId);

                var failure = WebhookFailure.Create(
                    provider: "stripe",
                    eventId: data.EventId,
                    eventType: "checkout.session.completed",
                    payload: $"CreatorId={creatorId}, SubscriptionId={subscriptionId}, SessionId={data.SessionId}, " +
                        $"StripeSubscriptionId={data.StripeSubscriptionId}, StripeCustomerId={data.StripeCustomerId}",
                    errorMessage: "Checkout was paid after the pending subscription was cancelled (the workspace " +
                        "switched to the Free plan). The paid plan was not activated — refund the charge and cancel " +
                        "the Stripe subscription.",
                    occurredAt: now);

                await _webhookFailureRepository.AddAsync(failure, ct);
                await _unitOfWork.SaveChangesAsync(ct);
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

            subscription.ActivateWithProvider(data.StripeSubscriptionId, now);

            // Kept as is when customer.subscription.created already stored a period that is not older.
            if (period is not null)
                subscription.AdvancePeriod(period.CurrentPeriodStart, period.CurrentPeriodEnd, now);

            if (!string.IsNullOrWhiteSpace(data.StripeCustomerId))
                creator.SetStripeCustomerId(data.StripeCustomerId, now);

            creator.Activate(now);

            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Creator activated via Stripe checkout. CreatorId: {CreatorId}, SubscriptionId: {SubscriptionId}, StripeSubscriptionId: {StripeSubscriptionId}",
                creatorId, subscriptionId, data.StripeSubscriptionId);
        }, ct);
    }

    public async Task HandleSubscriptionCreatedAsync(
        SubscriptionChangedData data,
        CancellationToken ct)
    {
        const string eventType = "customer.subscription.created";

        // Usually created arrives after checkout.session.completed linked the Stripe id: then it is just another
        // state update.
        var linked = await _creatorSubscriptionRepository
            .GetByProviderSubscriptionIdForUpdateAsync(data.StripeSubscriptionId, ct);
        if (linked is not null)
        {
            await ApplySubscriptionChangeAsync(linked, data, eventType, ct);
            return;
        }

        // Created before checkout completed: find our row through the metadata set on the Checkout session.
        if (!data.Metadata.TryGetValue("subscriptionId", out var subscriptionIdStr)
            || !int.TryParse(subscriptionIdStr, out var subscriptionId))
        {
            _logger.LogInformation(
                "No subscription found for {EventType}. StripeSubscriptionId: {StripeSubscriptionId} — likely a non-platform subscription, ignoring.",
                eventType, data.StripeSubscriptionId);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var subscription = await _creatorSubscriptionRepository.GetByIdForUpdateAsync(subscriptionId, ct);

            if (subscription is null
                || (data.Metadata.TryGetValue("creatorId", out var creatorIdStr)
                    && int.TryParse(creatorIdStr, out var creatorId)
                    && subscription.CreatorId != creatorId))
            {
                _logger.LogWarning(
                    "{EventType} references an unknown subscription. SubscriptionId: {SubscriptionId}, StripeSubscriptionId: {StripeSubscriptionId}",
                    eventType, subscriptionId, data.StripeSubscriptionId);
                return;
            }

            // The workspace switched to Free (continue-free). Nothing to do here; checkout.session.completed
            // records the actionable "paid after switching" failure.
            if (subscription.Status == CreatorSubscriptionStatus.Cancelled)
            {
                _logger.LogWarning(
                    "{EventType} for a cancelled pending subscription, ignoring. SubscriptionId: {SubscriptionId}, StripeSubscriptionId: {StripeSubscriptionId}",
                    eventType, subscriptionId, data.StripeSubscriptionId);
                return;
            }

            if (subscription.Status != CreatorSubscriptionStatus.PendingPayment)
            {
                _logger.LogWarning(
                    "{EventType} matched by metadata to a subscription that is not pending, ignoring. SubscriptionId: {SubscriptionId}, Status: {Status}",
                    eventType, subscriptionId, subscription.Status);
                return;
            }

            if (subscription.IsStaleProviderEvent(data.OccurredAt))
            {
                LogStaleEvent(eventType, data);
                return;
            }

            // Only the period: activation and linking the Stripe id stay with checkout.session.completed, the
            // single activator.
            if (data.CurrentPeriodStart is { } periodStart && data.CurrentPeriodEnd is { } periodEnd)
                subscription.AdvancePeriod(periodStart, periodEnd, now);
            subscription.RecordProviderEvent(data.OccurredAt, now);

            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Billing period recorded for a pending subscription before checkout completed. SubscriptionId: {SubscriptionId}, StripeSubscriptionId: {StripeSubscriptionId}",
                subscriptionId, data.StripeSubscriptionId);
        }, ct);
    }

    public async Task HandleSubscriptionUpdatedAsync(
        SubscriptionChangedData data,
        CancellationToken ct)
    {
        const string eventType = "customer.subscription.updated";

        var subscription = await _creatorSubscriptionRepository
            .GetByProviderSubscriptionIdForUpdateAsync(data.StripeSubscriptionId, ct);

        if (subscription is null)
        {
            _logger.LogInformation(
                "No subscription found for {EventType}. StripeSubscriptionId: {StripeSubscriptionId} — likely a non-platform subscription, ignoring.",
                eventType, data.StripeSubscriptionId);
            return;
        }

        await ApplySubscriptionChangeAsync(subscription, data, eventType, ct);
    }

    /// <summary>The shared customer.subscription.created/updated path for a subscription already linked to
    /// its Stripe id: rejects stale events, then applies status, period (never backwards), cancel flag and plan.</summary>
    private async Task ApplySubscriptionChangeAsync(
        CreatorSubscription subscription,
        SubscriptionChangedData data,
        string eventType,
        CancellationToken ct)
    {
        if (subscription.IsStaleProviderEvent(data.OccurredAt))
        {
            LogStaleEvent(eventType, data);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var plansChanged = false;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            switch (data.Status)
            {
                case "active":
                    if (data.CurrentPeriodStart is { } periodStart && data.CurrentPeriodEnd is { } periodEnd)
                        subscription.AdvancePeriod(periodStart, periodEnd, now);

                    if (subscription.Creator.Status != CreatorStatus.Active)
                        subscription.Creator.Activate(now);

                    if (data.CancelAtPeriodEnd && !subscription.CancelAtPeriodEnd)
                        subscription.ScheduleCancel(now);
                    else if (!data.CancelAtPeriodEnd && subscription.CancelAtPeriodEnd)
                        subscription.UndoScheduledCancel(now);

                    // Ažuriraj plan ako se promijenio (upgrade/downgrade)
                    if (!string.IsNullOrWhiteSpace(data.StripePriceId)
                        && data.StripePriceId != subscription.Plan.StripePriceId)
                    {
                        var newPlan = await _creatorPlanRepository
                            .GetByStripePriceIdAsync(data.StripePriceId, ct);

                        if (newPlan is not null)
                        {
                            subscription.UpdatePlan(newPlan, now);
                            _logger.LogInformation(
                                "Subscription plan updated. StripeSubscriptionId: {StripeSubscriptionId}, NewPlan: {NewPlan}",
                                data.StripeSubscriptionId, newPlan.Code);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Plan not found for StripePriceId: {StripePriceId}. Plan not updated.",
                                data.StripePriceId);

                            // Silently leaving this as a log line means the plan silently stops
                            // tracking Stripe's billing state with no way to notice or reprocess it
                            // later — record it the same way any other webhook failure is recorded,
                            // even though the outer handler never threw.
                            var failure = WebhookFailure.Create(
                                provider: "stripe",
                                eventId: data.EventId,
                                eventType: eventType,
                                payload: $"StripeSubscriptionId={data.StripeSubscriptionId}, " +
                                    $"StripePriceId={data.StripePriceId}, " +
                                    $"CurrentPlanCode={subscription.Plan.Code}",
                                errorMessage: $"No creator plan found for StripePriceId '{data.StripePriceId}'. " +
                                    "Subscription plan was left unchanged.",
                                occurredAt: now);

                            await _webhookFailureRepository.AddAsync(failure, ct);
                        }
                    }

                    _logger.LogInformation(
                        "Subscription updated via Stripe. StripeSubscriptionId: {StripeSubscriptionId}, CancelAtPeriodEnd: {CancelAtPeriodEnd}",
                        data.StripeSubscriptionId, data.CancelAtPeriodEnd);
                    break;

                case "past_due":
                    subscription.MarkPastDue(now);

                    _logger.LogWarning(
                        "Subscription marked past due. StripeSubscriptionId: {StripeSubscriptionId}",
                        data.StripeSubscriptionId);
                    break;

                case "canceled":
                case "cancelled":
                    // Same end as customer.subscription.deleted (Stripe sends both): the workspace moves to Free.
                    if (!await EndPaidSubscriptionAsync(subscription, eventType, data, now, ct))
                        return;
                    plansChanged = true;
                    break;

                default:
                    _logger.LogInformation(
                        "Unhandled subscription status in {EventType}. Status: {Status}, StripeSubscriptionId: {StripeSubscriptionId}",
                        eventType, data.Status, data.StripeSubscriptionId);
                    return;
            }

            subscription.RecordProviderEvent(data.OccurredAt, now);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        if (plansChanged)
            _cacheInvalidator.Invalidate(subscription.CreatorId);
    }

    /// <summary>
    /// The paid subscription has ended in Stripe (cancelled at period end, or the final payment failed). Inside the
    /// caller's transaction: the subscription is cancelled and the workspace moves to an Active Free subscription —
    /// its pages and products stay as they are; the Free limits only block creating or restoring beyond them. A
    /// deleted (Disabled) workspace stays deleted and gets no Free plan. Returns false when a concurrent event already
    /// ended this subscription (nothing to do).
    /// </summary>
    private async Task<bool> EndPaidSubscriptionAsync(
        CreatorSubscription subscription, string eventType, SubscriptionChangedData data, DateTimeOffset now,
        CancellationToken ct)
    {
        // customer.subscription.deleted and .updated(canceled) arrive together: serialize them on the row and re-read
        // its committed status, so only one of them creates the Free subscription.
        await _creatorSubscriptionRepository.LockForUpdateAsync(subscription.Id, ct);
        if (await _creatorSubscriptionRepository.GetStatusAsync(subscription.Id, ct) == CreatorSubscriptionStatus.Cancelled)
        {
            _logger.LogInformation(
                "Subscription already ended by a concurrent event, skipping {EventType}. StripeSubscriptionId: {StripeSubscriptionId}",
                eventType, data.StripeSubscriptionId);
            return false;
        }

        subscription.Cancel(now);
        var creator = subscription.Creator;

        if (creator.Status == CreatorStatus.Disabled)
        {
            _logger.LogInformation(
                "Paid subscription of a deleted workspace ended via {EventType}; no Free plan. CreatorId: {CreatorId}, StripeSubscriptionId: {StripeSubscriptionId}",
                eventType, creator.Id, data.StripeSubscriptionId);
            return true;
        }

        var freePlan = await _creatorPlanRepository.GetByCodeAsync(FreePlanCode, ct)
            ?? throw new InvalidOperationException("The Free plan is missing; cannot move the workspace to it.");
        await _creatorSubscriptionRepository.AddAsync(CreatorSubscription.CreateFree(creator, freePlan, now), ct);

        // Workspaces suspended by the old behaviour (before this change) come back too.
        if (creator.Status == CreatorStatus.Suspended)
            creator.Activate(now);

        _logger.LogWarning(
            "Paid subscription ended via {EventType}; workspace moved to the Free plan. CreatorId: {CreatorId}, StripeSubscriptionId: {StripeSubscriptionId}",
            eventType, creator.Id, data.StripeSubscriptionId);
        return true;
    }

    private async Task<SubscriptionBillingPeriodDto?> TryReadBillingPeriodAsync(
        string stripeSubscriptionId, string sessionId, CancellationToken ct)
    {
        try
        {
            return await _billingPeriodService.GetBillingPeriodAsync(stripeSubscriptionId, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Could not read the billing period from Stripe; activating without it. StripeSubscriptionId: {StripeSubscriptionId}, SessionId: {SessionId}",
                stripeSubscriptionId, sessionId);
            return null;
        }
    }

    private void LogStaleEvent(string eventType, SubscriptionChangedData data)
    {
        _logger.LogInformation(
            "Ignoring stale {EventType}: older than the last applied subscription event. EventId: {EventId}, StripeSubscriptionId: {StripeSubscriptionId}, OccurredAt: {OccurredAt}",
            eventType, data.EventId, data.StripeSubscriptionId, data.OccurredAt);
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

        if (subscription.IsStaleProviderEvent(data.OccurredAt))
        {
            LogStaleEvent("customer.subscription.deleted", data);
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
        var ended = false;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            ended = await EndPaidSubscriptionAsync(subscription, "customer.subscription.deleted", data, now, ct);
            if (!ended)
                return;

            subscription.RecordProviderEvent(data.OccurredAt, now);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        if (ended)
            _cacheInvalidator.Invalidate(subscription.CreatorId);
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

    public async Task HandleAccountUpdatedAsync(AccountUpdatedData data, CancellationToken ct)
    {
        var creator = await _creatorRepository.GetByStripeConnectAccountIdForUpdateAsync(data.AccountId, ct);
        if (creator is null)
        {
            _logger.LogInformation(
                "No creator found for account.updated. StripeConnectAccountId: {StripeConnectAccountId} — ignoring.",
                data.AccountId);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Stripe does not guarantee event delivery order — UpdateStripeConnectStatus no-ops if
            // data.OccurredAt is not newer than the last event we already applied.
            creator.UpdateStripeConnectStatus(
                data.DetailsSubmitted, data.ChargesEnabled, data.PayoutsEnabled, data.OccurredAt, now);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Creator Connect status updated. CreatorId: {CreatorId}, DetailsSubmitted: {DetailsSubmitted}, ChargesEnabled: {ChargesEnabled}, PayoutsEnabled: {PayoutsEnabled}",
                creator.Id, data.DetailsSubmitted, data.ChargesEnabled, data.PayoutsEnabled);
        }, ct);
    }
}
