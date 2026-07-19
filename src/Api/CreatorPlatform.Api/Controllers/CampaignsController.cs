using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/campaigns")]
public sealed class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly ICurrentUserContext _currentUserContext;

    public CampaignsController(ICampaignService campaignService, ICurrentUserContext currentUserContext)
    {
        _campaignService = campaignService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet("audience-preview")]
    public async Task<ActionResult<ApiResponse<AudiencePreviewDto>>> GetAudiencePreview(
        string slug,
        [FromQuery] CampaignAudienceType audienceType,
        [FromQuery] Guid targetPublicId,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var preview = await _campaignService.GetAudiencePreviewAsync(slug, currentUser.Id, audienceType, targetPublicId, ct);

        return Ok(ApiResponse<AudiencePreviewDto>.Success(
            StatusCodes.Status200OK,
            "Audience preview loaded.",
            preview));
    }

    /// <summary>Returns the authenticated user or throws 401.</summary>
    private CurrentUserDto GetAuthenticatedUser()
    {
        var user = _currentUserContext.User;
        if (user is null)
            throw new UnauthorizedException("Authentication is required.");
        return user;
    }

    /// <summary>Returns the authenticated and email-verified user or throws 401/403.</summary>
    private CurrentUserDto GetVerifiedUser()
    {
        var user = GetAuthenticatedUser();
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        return user;
    }
}
