using CreatorPlatform.Api.Responses;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/public/landing-pages")]
public sealed class PublicLandingPagesController : ControllerBase
{
    private readonly IPublicLandingPageService _publicLandingPageService;

    public PublicLandingPagesController(IPublicLandingPageService publicLandingPageService)
    {
        _publicLandingPageService = publicLandingPageService;
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

        return Ok(ApiResponse<LandingPageWithSectionsResponseDto>.Success(
            StatusCodes.Status200OK,
            "Page loaded.",
            page));
    }
}
