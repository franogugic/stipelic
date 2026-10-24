using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Domain;

namespace CreatorPlatform.Api.Webhooks;

public enum StripeWebhookOutcome
{
    Processed,

    /// <summary>Already handled by an earlier delivery: nothing ran.</summary>
    Duplicate,

    /// <summary>Another delivery is handling the event right now: nothing ran; answered 409 so Stripe tries again later.</summary>
    InProgress,

    /// <summary>Failed the same way every time: recorded, answered 200 — fixed and replayed by an admin.</summary>
    PermanentFailure,

    /// <summary>Failed on an outage: recorded, answered 500 so Stripe retries.</summary>
    TransientFailure
}

/// <summary>
/// Runs one verified Stripe event through its handler — for both webhook endpoints and for an admin's replay. The event id
/// is claimed first (<c>processed_webhook_events</c>), so a redelivered or concurrently delivered event runs once. A
/// failure gives the claim back, is recorded (one row per event, its attempts counted) and logged as
/// <see cref="FailedLogEvent"/> for alerting; a success resolves the event's earlier failure rows.
/// </summary>
public sealed class StripeWebhookProcessor
{
    public const string Provider = "stripe";

    /// <summary>The log event alerting watches: every failed webhook handler, with its Kind (Transient / Permanent).</summary>
    public static readonly EventId FailedLogEvent = new(9100, "StripeWebhookFailed");

    /// <summary>A claim not marked processed after this long belongs to a delivery that died; the next one takes over.</summary>
    public static readonly TimeSpan ClaimStaleAfter = TimeSpan.FromMinutes(5);

    private readonly ICreatorWebhookService _creatorWebhookService;
    private readonly IOrderWebhookService _orderWebhookService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StripeWebhookProcessor> _logger;

    public StripeWebhookProcessor(
        ICreatorWebhookService creatorWebhookService,
        IOrderWebhookService orderWebhookService,
        IServiceScopeFactory scopeFactory,
        ILogger<StripeWebhookProcessor> logger)
    {
        _creatorWebhookService = creatorWebhookService;
        _orderWebhookService = orderWebhookService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<StripeWebhookOutcome> ProcessAsync(StripeWebhookEventDto webhookEvent, string payload, CancellationToken ct)
    {
        WebhookEventClaim claim;
        try
        {
            claim = await UseStoreAsync(store => store.TryClaimAsync(
                Provider, webhookEvent.EventId, webhookEvent.EventType, DateTimeOffset.UtcNow, ClaimStaleAfter, CancellationToken.None));
        }
        catch (Exception exception)
        {
            // Without the claim nothing can run safely — and the database is most likely down: let Stripe retry.
            _logger.LogError(
                FailedLogEvent,
                exception,
                "Stripe webhook could not be claimed. Kind: {FailureKind}, EventId: {EventId}, EventType: {EventType}",
                "Transient",
                webhookEvent.EventId,
                webhookEvent.EventType);
            return StripeWebhookOutcome.TransientFailure;
        }

        if (claim == WebhookEventClaim.AlreadyProcessed)
        {
            _logger.LogInformation(
                "Stripe webhook already processed, skipping (idempotent). EventId: {EventId}, EventType: {EventType}",
                webhookEvent.EventId, webhookEvent.EventType);
            await ResolveEarlierFailuresAsync(webhookEvent);
            return StripeWebhookOutcome.Duplicate;
        }

        if (claim == WebhookEventClaim.InProgress)
        {
            _logger.LogInformation(
                "Stripe webhook is being processed by another delivery. EventId: {EventId}, EventType: {EventType}",
                webhookEvent.EventId, webhookEvent.EventType);
            return StripeWebhookOutcome.InProgress;
        }

        try
        {
            if (webhookEvent.ConnectedAccountId is null)
                await DispatchAsync(webhookEvent, ct);
            else
                await DispatchConnectAsync(webhookEvent, ct);
        }
        catch (Exception exception)
        {
            var transient = WebhookFailureClassifier.IsTransient(exception);
            await ReleaseClaimAsync(webhookEvent);
            var attempts = await RecordFailureAsync(webhookEvent, payload, exception);

            _logger.LogError(
                FailedLogEvent,
                exception,
                "Stripe webhook handler failed. Kind: {FailureKind}, EventId: {EventId}, EventType: {EventType}, Attempts: {Attempts}",
                transient ? "Transient" : "Permanent",
                webhookEvent.EventId,
                webhookEvent.EventType,
                attempts);

            return transient ? StripeWebhookOutcome.TransientFailure : StripeWebhookOutcome.PermanentFailure;
        }

        await MarkProcessedAsync(webhookEvent);
        await ResolveEarlierFailuresAsync(webhookEvent);
        return StripeWebhookOutcome.Processed;
    }

    private async Task<T> UseStoreAsync<T>(Func<IProcessedWebhookEventStore, Task<T>> action)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IProcessedWebhookEventStore>());
    }

    private async Task MarkProcessedAsync(StripeWebhookEventDto webhookEvent)
    {
        try
        {
            await UseStoreAsync(async store =>
            {
                await store.MarkProcessedAsync(webhookEvent.EventId, DateTimeOffset.UtcNow, CancellationToken.None);
                return true;
            });
        }
        catch (Exception exception)
        {
            // Handled, but not stamped: the claim goes stale, and a redelivery re-runs the (idempotent) handler.
            _logger.LogWarning(exception, "Could not mark the Stripe webhook processed. EventId: {EventId}", webhookEvent.EventId);
        }
    }

    private async Task ReleaseClaimAsync(StripeWebhookEventDto webhookEvent)
    {
        try
        {
            await UseStoreAsync(async store =>
            {
                await store.ReleaseAsync(webhookEvent.EventId, CancellationToken.None);
                return true;
            });
        }
        catch (Exception exception)
        {
            // The claim goes stale after ClaimStaleAfter; until then retries answer 409 and Stripe tries again later.
            _logger.LogWarning(exception, "Could not release the Stripe webhook claim. EventId: {EventId}", webhookEvent.EventId);
        }
    }

    // Connect events (connected-account lifecycle) come from their own endpoint and carry the account id.
    private async Task DispatchConnectAsync(StripeWebhookEventDto webhookEvent, CancellationToken ct)
    {
        switch (webhookEvent.EventType)
        {
            case StripeEventTypes.AccountUpdated when webhookEvent.AccountUpdated is not null:
                await _creatorWebhookService.HandleAccountUpdatedAsync(webhookEvent.AccountUpdated, ct);
                break;

            default:
                _logger.LogInformation(
                    "Unhandled Stripe Connect event type received, ignoring. EventId: {EventId}, EventType: {EventType}",
                    webhookEvent.EventId,
                    webhookEvent.EventType);
                break;
        }
    }

    private async Task DispatchAsync(StripeWebhookEventDto webhookEvent, CancellationToken ct)
    {
        switch (webhookEvent.EventType)
        {
            case StripeEventTypes.CheckoutSessionCompleted
                when webhookEvent.CheckoutSessionCompleted is not null:
                await _creatorWebhookService
                    .HandleCheckoutSessionCompletedAsync(webhookEvent.CheckoutSessionCompleted, ct);
                await _orderWebhookService
                    .HandleCheckoutSessionCompletedAsync(
                        new OrderCheckoutCompletedDto(
                            webhookEvent.CheckoutSessionCompleted.SessionId,
                            webhookEvent.CheckoutSessionCompleted.StripePaymentIntentId,
                            webhookEvent.CheckoutSessionCompleted.CustomerEmail,
                            webhookEvent.CheckoutSessionCompleted.CustomerName,
                            webhookEvent.CheckoutSessionCompleted.Metadata,
                            webhookEvent.CheckoutSessionCompleted.CustomerCountry),
                        ct);
                break;

            case StripeEventTypes.CheckoutSessionExpired
                when webhookEvent.CheckoutSessionExpired is not null:
                await _orderWebhookService
                    .HandleCheckoutSessionExpiredAsync(
                        new OrderCheckoutExpiredDto(
                            webhookEvent.CheckoutSessionExpired.SessionId,
                            webhookEvent.CheckoutSessionExpired.Metadata),
                        ct);
                break;

            case StripeEventTypes.CustomerSubscriptionCreated
                when webhookEvent.SubscriptionChanged is not null:
                await _creatorWebhookService
                    .HandleSubscriptionCreatedAsync(webhookEvent.SubscriptionChanged, ct);
                break;

            case StripeEventTypes.CustomerSubscriptionUpdated
                when webhookEvent.SubscriptionChanged is not null:
                await _creatorWebhookService
                    .HandleSubscriptionUpdatedAsync(webhookEvent.SubscriptionChanged, ct);
                break;

            case StripeEventTypes.CustomerSubscriptionDeleted
                when webhookEvent.SubscriptionChanged is not null:
                await _creatorWebhookService
                    .HandleSubscriptionDeletedAsync(webhookEvent.SubscriptionChanged, ct);
                break;

            case StripeEventTypes.InvoicePaymentFailed
                when webhookEvent.InvoicePaymentFailed is not null:
                await _creatorWebhookService
                    .HandleInvoicePaymentFailedAsync(webhookEvent.InvoicePaymentFailed, ct);
                break;

            case StripeEventTypes.ChargeRefunded
                when webhookEvent.ChargeRefunded is not null:
                await _orderWebhookService
                    .HandleChargeRefundedAsync(
                        new OrderChargeRefundedDto(
                            webhookEvent.ChargeRefunded.PaymentIntentId,
                            webhookEvent.ChargeRefunded.ChargeId,
                            webhookEvent.ChargeRefunded.AmountCents,
                            webhookEvent.ChargeRefunded.AmountRefundedCents,
                            webhookEvent.ChargeRefunded.FullyRefunded),
                        ct);
                break;

            default:
                _logger.LogInformation(
                    "Unhandled Stripe event type received, ignoring. EventId: {EventId}, EventType: {EventType}",
                    webhookEvent.EventId,
                    webhookEvent.EventType);
                break;
        }
    }

    /// <summary>Records the failed attempt on the event's open row (or a new one). Its own scope and DbContext: the
    /// request's may still hold the failed handler's unsaved changes. Never throws; returns the attempts, or null.</summary>
    private async Task<int?> RecordFailureAsync(StripeWebhookEventDto webhookEvent, string payload, Exception exception)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var failures = scope.ServiceProvider.GetRequiredService<IWebhookFailureRepository>();
            var now = DateTimeOffset.UtcNow;

            var failure = await failures.GetOpenHandlerFailureAsync(Provider, webhookEvent.EventId, CancellationToken.None);
            if (failure is null)
            {
                failure = WebhookFailure.Create(
                    Provider, webhookEvent.EventId, webhookEvent.EventType, payload, exception.ToString(), now,
                    WebhookFailureKind.HandlerFailed);
                await failures.AddAsync(failure, CancellationToken.None);
            }
            else
            {
                failure.RecordAttempt(exception.ToString(), now);
            }

            await failures.SaveChangesAsync(CancellationToken.None);
            return failure.Attempts;
        }
        catch (Exception persistException)
        {
            _logger.LogCritical(
                persistException,
                "Failed to persist webhook failure record. EventId: {EventId}, EventType: {EventType}",
                webhookEvent.EventId,
                webhookEvent.EventType);
            return null;
        }
    }

    private async Task ResolveEarlierFailuresAsync(StripeWebhookEventDto webhookEvent)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var failures = scope.ServiceProvider.GetRequiredService<IWebhookFailureRepository>();
            var resolved = await failures.ResolveOpenHandlerFailuresAsync(
                Provider, webhookEvent.EventId, DateTimeOffset.UtcNow, CancellationToken.None);

            if (resolved > 0)
            {
                _logger.LogInformation(
                    "Stripe webhook processed after earlier failures; resolved them. EventId: {EventId}, Resolved: {Resolved}",
                    webhookEvent.EventId, resolved);
            }
        }
        catch (Exception exception)
        {
            // The event itself succeeded; a stale open row only costs an admin a no-op replay.
            _logger.LogWarning(exception, "Could not resolve earlier webhook failures. EventId: {EventId}", webhookEvent.EventId);
        }
    }
}
