using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/public/landing-pages")]
public sealed class PublicLandingPagesController : ControllerBase
{
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

    public PublicLandingPagesController(
        IPublicLandingPageService publicLandingPageService,
        IPageViewService pageViewService)
    {
        _publicLandingPageService = publicLandingPageService;
        _pageViewService = pageViewService;
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
                Code = "NOT_FOUND"
            });

        var visitorId = ResolveVisitorId();
        Response.Cookies.Append(VisitorCookieName, visitorId.ToString(), VisitorCookieOptions);

        await _pageViewService.RecordAsync(page.InternalId, visitorId, ct);

        return Ok(ApiResponse<LandingPageWithSectionsResponseDto>.Success(
            StatusCodes.Status200OK,
            "Page loaded.",
            page));
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
