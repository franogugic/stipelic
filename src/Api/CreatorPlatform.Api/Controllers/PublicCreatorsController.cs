using CreatorPlatform.Api.Responses;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/public/creators")]
public sealed class PublicCreatorsController : ControllerBase
{
    private readonly IPublicLandingPageService _publicLandingPageService;

    public PublicCreatorsController(IPublicLandingPageService publicLandingPageService)
    {
        _publicLandingPageService = publicLandingPageService;
    }

    /// <summary>"More from {brand}" on a missing page: the creator's brand and up to six published pages.</summary>
    [HttpGet("{creatorSlug}/pages")]
    [EnableRateLimiting("PublicCreatorPages")]
    public async Task<ActionResult<ApiResponse<PublicCreatorPagesResponseDto>>> GetPages(string creatorSlug, CancellationToken ct)
    {
        var result = await _publicLandingPageService.GetCreatorPagesAsync(creatorSlug, ct);
        if (result is null)
            return NotFound(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Message = "Creator not found.",
                Code = "CREATOR_NOT_FOUND"
            });

        return Ok(ApiResponse<PublicCreatorPagesResponseDto>.Success(StatusCodes.Status200OK, "Pages loaded.", result));
    }
}
