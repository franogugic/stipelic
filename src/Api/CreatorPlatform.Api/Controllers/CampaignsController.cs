using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/campaigns")]
public sealed class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly ICampaignSendService _campaignSendService;
    private readonly ICurrentUserContext _currentUserContext;

    public CampaignsController(
        ICampaignService campaignService,
        ICampaignSendService campaignSendService,
        ICurrentUserContext currentUserContext)
    {
        _campaignService = campaignService;
        _campaignSendService = campaignSendService;
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

    /// <summary>Send history — last 50 sends with progress.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CampaignListItemDto>>>> List(string slug, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var campaigns = await _campaignService.ListAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<List<CampaignListItemDto>>.Success(
            StatusCodes.Status200OK,
            "Campaigns loaded.",
            campaigns));
    }

    [HttpGet("{campaignPublicId:guid}")]
    public async Task<ActionResult<ApiResponse<CampaignDetailDto>>> Get(string slug, Guid campaignPublicId, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var campaign = await _campaignService.GetAsync(slug, currentUser.Id, campaignPublicId, ct);

        return Ok(ApiResponse<CampaignDetailDto>.Success(
            StatusCodes.Status200OK,
            "Campaign loaded.",
            campaign));
    }

    [HttpGet("{campaignPublicId:guid}/failed-recipients")]
    public async Task<ActionResult<ApiResponse<List<FailedRecipientDto>>>> GetFailedRecipients(
        string slug, Guid campaignPublicId, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var recipients = await _campaignService.GetFailedRecipientsAsync(slug, currentUser.Id, campaignPublicId, ct);

        return Ok(ApiResponse<List<FailedRecipientDto>>.Success(
            StatusCodes.Status200OK,
            "Failed recipients loaded.",
            recipients));
    }

    /// <summary>Sends an Active template to an audience — creates a new Queued send record directly (no
    /// Draft step; see 02R rework).</summary>
    [HttpPost("send")]
    [EnableRateLimiting("SendCampaign")]
    public async Task<ActionResult<ApiResponse<CampaignDetailDto>>> Send(
        string slug, [FromBody] SendCampaignRequestDto request, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var campaign = await _campaignSendService.SendAsync(slug, currentUser.Id, request, ct);

        return Ok(ApiResponse<CampaignDetailDto>.Success(
            StatusCodes.Status200OK,
            "Campaign queued for sending.",
            campaign));
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
