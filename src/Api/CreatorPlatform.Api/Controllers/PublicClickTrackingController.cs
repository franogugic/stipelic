using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Api.Controllers;

/// <summary>Unauthenticated tracked links in campaign emails: records the click and redirects to the link's
/// destination. Known limitation: clicks are an estimate — security scanners in some mail systems open links before
/// the person does, so they over-report somewhat.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/c")]
public sealed class PublicClickTrackingController : ControllerBase
{
    private readonly IClickTrackingService _clickTrackingService;
    private readonly MarketingOptions _options;
    private readonly ILogger<PublicClickTrackingController> _logger;

    public PublicClickTrackingController(
        IClickTrackingService clickTrackingService,
        IOptions<MarketingOptions> options,
        ILogger<PublicClickTrackingController> logger)
    {
        _clickTrackingService = clickTrackingService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Always a 302: to the link's destination, read from the database by the signed link id (never taken from the
    /// request, so this is not an open redirect), or to the web app's not-found page for an invalid token, a link
    /// that is not the recipient's, or a failing lookup. Not rate limited: corporate link scanners click through
    /// shared IPs, and a per-IP limit would answer JSON instead of the redirect. Safe without a limit: a forged token
    /// is rejected by the HMAC check before any database query, and a valid one only bumps that recipient's counters.
    /// </summary>
    [HttpGet("{token}")]
    [DisableRateLimiting]
    public async Task<IActionResult> Get(string token, CancellationToken ct)
    {
        string? target = null;
        try
        {
            target = await _clickTrackingService.ResolveClickAsync(token, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Tracked link lookup failed; sending the visitor to the not-found page.");
        }

        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return Redirect(target ?? $"{_options.FrontendBaseUrl.TrimEnd('/')}/not-found");
    }
}
