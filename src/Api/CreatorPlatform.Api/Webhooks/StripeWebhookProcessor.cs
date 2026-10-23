using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Domain;

namespace CreatorPlatform.Api.Webhooks;

public enum StripeWebhookOutcome
{
    Processed,

    /// <summary>Failed the same way every time: recorded, answered 200 — fixed and replayed by an admin.</summary>
    PermanentFailure,

    /// <summary>Failed on an outage: recorded, answered 500 so Stripe retries.</summary>
    TransientFailure
}

/// <summary>
/// Runs one verified Stripe event through its handler — for both webhook endpoints and for an admin's replay. A failure
/// is recorded (one row per event, its attempts counted) and logged as <see cref="FailedLogEvent"/> for alerting; a
/// success resolves the event's earlier failure rows.
/// </summary>
public sealed class StripeWebhookProcessor
{
    public const string Provider = "stripe";

    /// <summary>The log event alerting watches: every failed webhook handler, with its Kind (Transient / Permanent).</summary>
    public static readonly EventId FailedLogEvent = new(9100, "StripeWebhookFailed");

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

        await ResolveEarlierFailuresAsync(webhookEvent);
        return StripeWebhookOutcome.Processed;
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
                            webhookEvent.CheckoutSessionCompleted.Metadata),
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
