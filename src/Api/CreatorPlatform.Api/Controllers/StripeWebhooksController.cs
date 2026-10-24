using CreatorPlatform.Api.Webhooks;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

/// <summary>
/// Stripe's two webhook endpoints. Both verify the signature, then hand the event to <see cref="StripeWebhookProcessor"/>:
/// 200 when it was handled (now or by an earlier delivery) or failed permanently (recorded for an admin's replay); 500
/// when it failed on an outage and 409 while another delivery handles it, so Stripe retries.
/// </summary>
[ApiController]
[Route("webhooks/stripe")]
[DisableRateLimiting]
public sealed class StripeWebhooksController : ControllerBase
{
    private readonly IStripeWebhookService _stripeWebhookService;
    private readonly StripeWebhookProcessor _processor;
    private readonly ILogger<StripeWebhooksController> _logger;

    public StripeWebhooksController(
        IStripeWebhookService stripeWebhookService,
        StripeWebhookProcessor processor,
        ILogger<StripeWebhooksController> logger)
    {
        _stripeWebhookService = stripeWebhookService;
        _processor = processor;
        _logger = logger;
    }

    [HttpPost]
    public Task<IActionResult> Handle(CancellationToken ct)
        => HandleAsync(_stripeWebhookService.ParseAndVerify, "Stripe", ct);

    // Connect events (connected-account lifecycle, e.g. account.updated) are registered as a separate
    // webhook endpoint in Stripe with their own signing secret — hence a dedicated route + verify call.
    [HttpPost("connect")]
    public Task<IActionResult> HandleConnect(CancellationToken ct)
        => HandleAsync(_stripeWebhookService.ParseAndVerifyConnect, "Stripe Connect", ct);

    private async Task<IActionResult> HandleAsync(
        Func<string, string, StripeWebhookEventDto> parseAndVerify, string endpoint, CancellationToken ct)
    {
        string payload;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            payload = await reader.ReadToEndAsync(ct);
        }

        var stripeSignature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(stripeSignature))
        {
            _logger.LogWarning("{Endpoint} webhook received without Stripe-Signature header.", endpoint);
            return BadRequest("Missing Stripe-Signature header.");
        }

        StripeWebhookEventDto webhookEvent;
        try
        {
            webhookEvent = parseAndVerify(payload, stripeSignature);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex, "{Endpoint} webhook verification failed.", endpoint);
            return BadRequest(ex.Message);
        }

        var outcome = await _processor.ProcessAsync(webhookEvent, payload, ct);

        // Only an outage (or a delivery still running elsewhere) is worth Stripe's retries; a permanent failure is
        // recorded and waits for a fix and a replay; a duplicate was already handled.
        return outcome switch
        {
            StripeWebhookOutcome.TransientFailure => StatusCode(StatusCodes.Status500InternalServerError),
            StripeWebhookOutcome.InProgress => Conflict(),
            _ => Ok()
        };
    }
}
