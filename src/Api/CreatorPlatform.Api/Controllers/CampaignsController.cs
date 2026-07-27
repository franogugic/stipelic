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

    [HttpGet("audience-preview/recipients")]
    public async Task<ActionResult<ApiResponse<AudienceRecipientsPageDto>>> GetAudienceRecipients(
        string slug,
        [FromQuery] CampaignAudienceType audienceType,
        [FromQuery] Guid targetPublicId,
        [FromQuery] string? afterEmail,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var page = await _campaignService.GetAudienceRecipientsAsync(
            slug, currentUser.Id, audienceType, targetPublicId, afterEmail, limit, ct);

        return Ok(ApiResponse<AudienceRecipientsPageDto>.Success(
            StatusCodes.Status200OK,
            "Audience recipients loaded.",
            page));
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

    /// <summary>Manually requeues every currently-Failed recipient of this send — see
    /// <see cref="ICampaignSendService.ResendFailedAsync"/> for why this never touches the monthly usage
    /// counter.</summary>
    [HttpPost("{campaignPublicId:guid}/resend-failed")]
    [EnableRateLimiting("ResendFailedCampaign")]
    public async Task<ActionResult<ApiResponse<ResendFailedResultDto>>> ResendFailed(
        string slug, Guid campaignPublicId, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var result = await _campaignSendService.ResendFailedAsync(slug, currentUser.Id, campaignPublicId, ct);

        return Ok(ApiResponse<ResendFailedResultDto>.Success(
            StatusCodes.Status200OK,
            result.RequeuedCount == 0 ? "No failed recipients to resend." : $"{result.RequeuedCount} recipient(s) requeued.",
            result));
    }

    /// <summary>Cancels a still-Scheduled send before it dispatches — never touches an already-Queued/
    /// Failed/Cancelled campaign (409 if attempted).</summary>
    [HttpDelete("{campaignPublicId:guid}/schedule")]
    public async Task<ActionResult<ApiResponse<CampaignDetailDto>>> CancelSchedule(
        string slug, Guid campaignPublicId, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var campaign = await _campaignSendService.CancelScheduledAsync(slug, currentUser.Id, campaignPublicId, ct);

        return Ok(ApiResponse<CampaignDetailDto>.Success(
            StatusCodes.Status200OK,
            "Scheduled send cancelled.",
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
