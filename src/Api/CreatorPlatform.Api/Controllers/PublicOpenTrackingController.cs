using CreatorPlatform.Marketing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

/// <summary>Unauthenticated open-tracking pixel embedded in campaign emails. Known limitation: opens are an
/// estimate, not a fact — Apple Mail Privacy Protection and mail-provider image proxies pre-fetch images,
/// which inflates the count for some recipients, while clients that block images hide real opens.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/o")]
public sealed class PublicOpenTrackingController : ControllerBase
{
    /// <summary>The smallest valid transparent 1x1 GIF.</summary>
    private static readonly byte[] TransparentGif = Convert.FromBase64String(
        "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    private readonly IOpenTrackingService _openTrackingService;
    private readonly ILogger<PublicOpenTrackingController> _logger;

    public PublicOpenTrackingController(
        IOpenTrackingService openTrackingService,
        ILogger<PublicOpenTrackingController> logger)
    {
        _openTrackingService = openTrackingService;
        _logger = logger;
    }

    /// <summary>Always answers with the same GIF — for a valid token, a forged one, an unknown recipient and
    /// a failing database alike — so the endpoint can't be used to probe which recipient ids exist.</summary>
    [HttpGet("{token}.gif")]
    [EnableRateLimiting("OpenPixel")]
    public async Task<IActionResult> Get(string token, CancellationToken ct)
    {
        try
        {
            await _openTrackingService.RecordOpenAsync(token, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Open tracking pixel request failed; serving the pixel anyway.");
        }

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";
        return File(TransparentGif, "image/gif");
    }
}
