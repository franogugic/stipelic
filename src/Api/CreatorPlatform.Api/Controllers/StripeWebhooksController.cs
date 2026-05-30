using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
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
    private readonly ILogger<StripeWebhooksController> _logger;

    public StripeWebhooksController(
        IStripeWebhookService stripeWebhookService,
        ICreatorWebhookService creatorWebhookService,
        ILogger<StripeWebhooksController> logger)
    {
        _stripeWebhookService = stripeWebhookService;
        _creatorWebhookService = creatorWebhookService;
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
                "Unhandled exception while processing Stripe webhook. EventId: {EventId}, EventType: {EventType}",
                webhookEvent.EventId,
                webhookEvent.EventType);

            // Return 500 so Stripe retries the event
            return StatusCode(StatusCodes.Status500InternalServerError);
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
}
