using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Domain;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("webhooks/stripe")]
[DisableRateLimiting]
public sealed class StripeWebhooksController : ControllerBase
{
    private readonly IStripeWebhookService _stripeWebhookService;
    private readonly ICreatorWebhookService _creatorWebhookService;
    private readonly IWebhookFailureRepository _webhookFailureRepository;
    private readonly ILogger<StripeWebhooksController> _logger;

    public StripeWebhooksController(
        IStripeWebhookService stripeWebhookService,
        ICreatorWebhookService creatorWebhookService,
        IWebhookFailureRepository webhookFailureRepository,
        ILogger<StripeWebhooksController> logger)
    {
        _stripeWebhookService = stripeWebhookService;
        _creatorWebhookService = creatorWebhookService;
        _webhookFailureRepository = webhookFailureRepository;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        string payload;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            payload = await reader.ReadToEndAsync(ct);
        }

        var stripeSignature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(stripeSignature))
        {
            _logger.LogWarning("Stripe webhook received without Stripe-Signature header.");
            return BadRequest("Missing Stripe-Signature header.");
        }

        StripeWebhookEventDto webhookEvent;
        try
        {
            webhookEvent = _stripeWebhookService.ParseAndVerify(payload, stripeSignature);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook verification failed.");
            return BadRequest(ex.Message);
        }

        try
        {
            await DispatchAsync(webhookEvent, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Stripe webhook handler failed. EventId: {EventId}, EventType: {EventType}",
                webhookEvent.EventId,
                webhookEvent.EventType);

            await PersistFailureAsync(webhookEvent, payload, ex, ct);

            // Vraćamo 200 da Stripe ne retryja — event je zapisan u bazu za ručni reprocessing
            return Ok();
        }

        return Ok();
    }

    private async Task DispatchAsync(StripeWebhookEventDto webhookEvent, CancellationToken ct)
    {
        switch (webhookEvent.EventType)
        {
            case StripeEventTypes.CheckoutSessionCompleted
                when webhookEvent.CheckoutSessionCompleted is not null:
                await _creatorWebhookService
                    .HandleCheckoutSessionCompletedAsync(webhookEvent.CheckoutSessionCompleted, ct);
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

            default:
                _logger.LogInformation(
                    "Unhandled Stripe event type received, ignoring. EventId: {EventId}, EventType: {EventType}",
                    webhookEvent.EventId,
                    webhookEvent.EventType);
                break;
        }
    }

    private async Task PersistFailureAsync(
        StripeWebhookEventDto webhookEvent,
        string payload,
        Exception ex,
        CancellationToken ct)
    {
        try
        {
            var failure = WebhookFailure.Create(
                provider: "stripe",
                eventId: webhookEvent.EventId,
                eventType: webhookEvent.EventType,
                payload: payload,
                errorMessage: ex.ToString(),
                occurredAt: DateTimeOffset.UtcNow);

            await _webhookFailureRepository.AddAsync(failure, ct);
            await _webhookFailureRepository.SaveChangesAsync(ct);
        }
        catch (Exception persistEx)
        {
            // Ako i ovo failna, barem logiramo — ne smijemo bacat exception iz error handlera
            _logger.LogCritical(
                persistEx,
                "Failed to persist webhook failure record. EventId: {EventId}, EventType: {EventType}",
                webhookEvent.EventId,
                webhookEvent.EventType);
        }
    }
}
