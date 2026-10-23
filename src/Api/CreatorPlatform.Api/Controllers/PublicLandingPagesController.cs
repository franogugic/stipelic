using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/public/landing-pages")]
public sealed class PublicLandingPagesController : ControllerBase
{
    // An unknown page and an unpublished one answer the same, so a draft's URL is never revealed.
    public const string PageNotFoundCode = "PAGE_NOT_FOUND";

    private const string VisitorCookieName = "lp_visitor_id";
    private static readonly CookieOptions VisitorCookieOptions = new()
    {
        HttpOnly = false,
        Secure = false,
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromDays(365)
    };

    private readonly IPublicLandingPageService _publicLandingPageService;
    private readonly IPageViewService _pageViewService;
    private readonly IEmailCaptureService _emailCaptureService;
    private readonly IOrderCheckoutService _orderCheckoutService;
    private readonly CaptureEmailRateLimiter _captureEmailRateLimiter;
    private readonly ILogger<PublicLandingPagesController> _logger;

    public PublicLandingPagesController(
        IPublicLandingPageService publicLandingPageService,
        IPageViewService pageViewService,
        IEmailCaptureService emailCaptureService,
        IOrderCheckoutService orderCheckoutService,
        CaptureEmailRateLimiter captureEmailRateLimiter,
        ILogger<PublicLandingPagesController> logger)
    {
        _publicLandingPageService = publicLandingPageService;
        _pageViewService = pageViewService;
        _emailCaptureService = emailCaptureService;
        _orderCheckoutService = orderCheckoutService;
        _captureEmailRateLimiter = captureEmailRateLimiter;
        _logger = logger;
    }

    [HttpGet("{creatorSlug}/{landingPageSlug}")]
    public async Task<ActionResult<ApiResponse<LandingPageWithSectionsResponseDto>>> Get(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct)
    {
        var page = await _publicLandingPageService.GetPublishedAsync(creatorSlug, landingPageSlug, ct);
        if (page is null)
            return NotFound(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Message = "Page not found.",
                Code = PageNotFoundCode
            });

        var visitorId = ResolveVisitorId();
        Response.Cookies.Append(VisitorCookieName, visitorId.ToString(), VisitorCookieOptions);

        await _pageViewService.RecordAsync(page.Id, visitorId, ct);

        return Ok(ApiResponse<LandingPageWithSectionsResponseDto>.Success(
            StatusCodes.Status200OK,
            "Page loaded.",
            page));
    }

    /// <summary>A lead sign-up: limited per IP (<c>CaptureSignup</c>) and per email address (<c>CaptureSignupPerEmail</c>);
    /// a filled-in honeypot answers 200 and does nothing.</summary>
    [HttpPost("{creatorSlug}/{landingPageSlug}/captures")]
    [EnableRateLimiting(RateLimitPolicies.CaptureSignup)]
    public async Task<ActionResult<ApiResponse<object>>> Capture(
        string creatorSlug,
        string landingPageSlug,
        [FromBody] EmailCaptureRequestDto request,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(request.Website))
        {
            _logger.LogInformation(
                "Lead sign-up with the honeypot filled in, ignored. Creator: {CreatorSlug}, Page: {LandingPageSlug}",
                creatorSlug, landingPageSlug);
            return Ok(ApiResponse<object>.Success(StatusCodes.Status200OK, "Email captured.", null));
        }

        using var lease = await _captureEmailRateLimiter.AcquireAsync(request.Email, ct);
        if (!lease.IsAcquired)
            return StatusCode(StatusCodes.Status429TooManyRequests, RateLimitRejection.Body(Response, lease));

        var page = await _publicLandingPageService.GetPublishedAsync(creatorSlug, landingPageSlug, ct);
        if (page is null)
            return NotFound(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Message = "Page not found.",
                Code = PageNotFoundCode
            });

        await _emailCaptureService.CaptureAsync(page.Id, page.ProductId, page.CreatorId, request.Email, ct);

        return Ok(ApiResponse<object>.Success(StatusCodes.Status200OK, "Email captured.", null));
    }

    [HttpPost("{creatorSlug}/{landingPageSlug}/checkout")]
    [EnableRateLimiting(RateLimitPolicies.StartCheckout)]
    public async Task<ActionResult<ApiResponse<CreateCheckoutResultDto>>> Checkout(
        string creatorSlug,
        string landingPageSlug,
        [FromBody] CreateCheckoutRequestDto request,
        CancellationToken ct)
    {
        var result = await _orderCheckoutService.CreateCheckoutAsync(creatorSlug, landingPageSlug, request.Email, ct);

        return Ok(ApiResponse<CreateCheckoutResultDto>.Success(
            StatusCodes.Status200OK,
            "Checkout session created.",
            result));
    }

    private Guid ResolveVisitorId()
    {
        if (Request.Cookies.TryGetValue(VisitorCookieName, out var raw) &&
            Guid.TryParse(raw, out var existing))
        {
            return existing;
        }

        return Guid.NewGuid();
    }
}
