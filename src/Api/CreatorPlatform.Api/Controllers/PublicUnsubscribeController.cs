using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

/// <summary>Unauthenticated, no-session endpoints reached from links inside actual emails — GDPR-required
/// unsubscribe. Both actions are creator-scoped: unsubscribing suppresses ALL of that creator's future
/// campaigns for this email, not just the one the link came from.</summary>
[ApiController]
[Route("api/public/unsubscribe")]
public sealed class PublicUnsubscribeController : ControllerBase
{
    private readonly IUnsubscribeTokenService _tokenService;
    private readonly IUnsubscribeRepository _unsubscribeRepository;

    public PublicUnsubscribeController(
        IUnsubscribeTokenService tokenService,
        IUnsubscribeRepository unsubscribeRepository)
    {
        _tokenService = tokenService;
        _unsubscribeRepository = unsubscribeRepository;
    }

    [HttpGet("{token}")]
    [EnableRateLimiting("Unsubscribe")]
    public async Task<ContentResult> Get(string token, CancellationToken ct)
    {
        await UnsubscribeAsync(token, UnsubscribeSource.Link, ct);

        return Content(BuildHtmlPage("You've been unsubscribed."), "text/html");
    }

    /// <summary>RFC 8058 one-click unsubscribe — mail clients POST here on the recipient's behalf without
    /// any confirmation UI, so this must never require anything beyond the token itself.</summary>
    [HttpPost("{token}")]
    [EnableRateLimiting("Unsubscribe")]
    public async Task<ContentResult> Post(string token, CancellationToken ct)
    {
        await UnsubscribeAsync(token, UnsubscribeSource.OneClick, ct);

        return Content(BuildHtmlPage("You've been unsubscribed."), "text/html");
    }

    private async Task UnsubscribeAsync(string token, UnsubscribeSource source, CancellationToken ct)
    {
        var payload = _tokenService.TryParse(token);
        if (payload is null)
            throw new BadRequestException("This unsubscribe link is invalid or has expired.");

        var unsubscribe = Unsubscribe.Create(payload.CreatorId, payload.Email, source, DateTimeOffset.UtcNow);
        await _unsubscribeRepository.AddIfNotExistsAsync(unsubscribe, ct);
    }

    private const string HtmlPageTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <title>Unsubscribed</title>
            <style>
                body {
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif;
                    background: #0A0A0B;
                    color: #F5F5F4;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    min-height: 100vh;
                    margin: 0;
                }
                .card {
                    max-width: 420px;
                    text-align: center;
                    padding: 32px;
                }
                p {
                    color: rgba(245, 245, 244, 0.7);
                    line-height: 1.5;
                }
            </style>
        </head>
        <body>
            <div class="card">
                <p>__MESSAGE__</p>
                <p>You will no longer receive promotional emails from this creator.</p>
            </div>
        </body>
        </html>
        """;

    private static string BuildHtmlPage(string message)
    {
        return HtmlPageTemplate.Replace("__MESSAGE__", message);
    }
}
