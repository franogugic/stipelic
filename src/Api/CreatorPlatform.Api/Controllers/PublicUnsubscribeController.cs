using CreatorPlatform.Api.Responses;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

/// <summary>Unauthenticated, no-session endpoints reached from links inside actual emails — GDPR-required
/// unsubscribe. Creator-scoped: unsubscribing suppresses ALL of that creator's future campaigns for this email, not
/// just the one the link came from. Nothing here unsubscribes on a GET, so link scanners that prefetch email links
/// can't unsubscribe anyone.</summary>
[ApiController]
[Route("api/public/unsubscribe")]
public sealed class PublicUnsubscribeController : ControllerBase
{
    private readonly IUnsubscribeTokenService _tokenService;
    private readonly IUnsubscribeRepository _unsubscribeRepository;
    private readonly IUnsubscribePageService _unsubscribePageService;

    public PublicUnsubscribeController(
        IUnsubscribeTokenService tokenService,
        IUnsubscribeRepository unsubscribeRepository,
        IUnsubscribePageService unsubscribePageService)
    {
        _tokenService = tokenService;
        _unsubscribeRepository = unsubscribeRepository;
        _unsubscribePageService = unsubscribePageService;
    }

    /// <summary>Links in emails sent before the unsubscribe page existed point here: forward them to the page (302),
    /// which asks for a confirmation. No side effect.</summary>
    [HttpGet("{token}")]
    [EnableRateLimiting("Unsubscribe")]
    public RedirectResult LegacyLink(string token) => Redirect(_tokenService.BuildUnsubscribePageUrl(token));

    /// <summary>The page's data: the creator's brand and whether the address is already unsubscribed. No side effect.
    /// 404 <c>unsubscribe_link_invalid</c> for a bad token.</summary>
    [HttpGet("{token}/info")]
    [EnableRateLimiting("Unsubscribe")]
    public async Task<ActionResult<ApiResponse<UnsubscribePageDto>>> Info(string token, CancellationToken ct)
    {
        var page = await _unsubscribePageService.GetInfoAsync(token, ct);
        return Ok(ApiResponse<UnsubscribePageDto>.Success(StatusCodes.Status200OK, "Unsubscribe link loaded.", page));
    }

    /// <summary>The page's "Unsubscribe" button. Idempotent; 404 <c>unsubscribe_link_invalid</c> for a bad token.</summary>
    [HttpPost("{token}/confirm")]
    [EnableRateLimiting("Unsubscribe")]
    public async Task<ActionResult<ApiResponse<UnsubscribePageDto>>> Confirm(string token, CancellationToken ct)
    {
        var page = await _unsubscribePageService.ConfirmAsync(token, ct);
        return Ok(ApiResponse<UnsubscribePageDto>.Success(StatusCodes.Status200OK, "Unsubscribed.", page));
    }

    /// <summary>RFC 8058 one-click unsubscribe (the List-Unsubscribe header) — mail clients POST here on the
    /// recipient's behalf without any confirmation UI, so this must never require anything beyond the token itself.
    /// No body is returned: nobody reads it.</summary>
    [HttpPost("{token}")]
    [EnableRateLimiting("Unsubscribe")]
    public async Task<IActionResult> OneClick(string token, CancellationToken ct)
    {
        var payload = _tokenService.TryParse(token)
            ?? throw new BadRequestException("This unsubscribe link is invalid or has expired.");

        await _unsubscribeRepository.AddIfNotExistsAsync(
            Unsubscribe.Create(payload.CreatorId, payload.Email, UnsubscribeSource.OneClick, DateTimeOffset.UtcNow), ct);

        return Ok();
    }
}
