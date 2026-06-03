using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/landing-pages")]
public sealed class LandingPagesController : ControllerBase
{
    private readonly ILandingPageService _landingPageService;
    private readonly ICurrentUserContext _currentUserContext;

    public LandingPagesController(
        ILandingPageService landingPageService,
        ICurrentUserContext currentUserContext)
    {
        _landingPageService = landingPageService;
        _currentUserContext = currentUserContext;
    }

    [HttpPost]
    [EnableRateLimiting("CreateLandingPage")]
    public async Task<ActionResult<ApiResponse<LandingPageResponseDto>>> Create(
        string slug,
        [FromBody] CreateLandingPageRequestDto request,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        var landingPage = await _landingPageService.CreateAsync(slug, user.Id, request, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<LandingPageResponseDto>.Success(
            StatusCodes.Status201Created,
            "Landing page created.",
            landingPage));
    }

    private Auth.Application.Dtos.CurrentUserDto GetVerifiedUser()
    {
        var user = _currentUserContext.User;
        if (user is null)
            throw new UnauthorizedException("Authentication is required.");
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        return user;
    }
}
