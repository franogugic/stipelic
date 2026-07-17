using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}")]
public sealed class CreatorPayoutsController : ControllerBase
{
    private readonly ICreatorPayoutService _creatorPayoutService;
    private readonly ICreatorService _creatorService;
    private readonly ICurrentUserContext _currentUserContext;

    public CreatorPayoutsController(
        ICreatorPayoutService creatorPayoutService,
        ICreatorService creatorService,
        ICurrentUserContext currentUserContext)
    {
        _creatorPayoutService = creatorPayoutService;
        _creatorService = creatorService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet("payouts/summary")]
    public async Task<ActionResult<ApiResponse<CreatorPayoutSummaryDto?>>> Summary(string slug, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var summary = await _creatorPayoutService.GetSummaryAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<CreatorPayoutSummaryDto?>.Success(
            StatusCodes.Status200OK,
            "Payout summary loaded.",
            summary));
    }

    [HttpGet("payouts")]
    public async Task<ActionResult<ApiResponse<List<PayoutDto>>>> History(string slug, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var payouts = await _creatorPayoutService.GetHistoryAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<List<PayoutDto>>.Success(
            StatusCodes.Status200OK,
            "Payout history loaded.",
            payouts));
    }

    [HttpPost("payouts/request")]
    [EnableRateLimiting("RequestPayout")]
    public async Task<ActionResult<ApiResponse<PayoutDto>>> RequestPayout(
        string slug,
        [FromBody] RequestPayoutRequestDto request,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var payout = await _creatorPayoutService.RequestPayoutAsync(slug, currentUser.Id, request.AmountCents, ct);

        var apiResponse = ApiResponse<PayoutDto>.Success(
            StatusCodes.Status201Created,
            "Payout requested.",
            payout);

        return Created($"/api/creators/{slug}/payouts/{payout.PublicId}", apiResponse);
    }

    [HttpDelete("payouts/{payoutPublicId:guid}")]
    public async Task<ActionResult<ApiResponse<PayoutDto>>> CancelPayoutRequest(
        string slug,
        Guid payoutPublicId,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var payout = await _creatorPayoutService.CancelPayoutRequestAsync(slug, currentUser.Id, payoutPublicId, ct);

        return Ok(ApiResponse<PayoutDto>.Success(
            StatusCodes.Status200OK,
            "Payout request cancelled.",
            payout));
    }

    [HttpGet("payout-profile")]
    public async Task<ActionResult<ApiResponse<PayoutProfileResponseDto?>>> GetPayoutProfile(string slug, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var profile = await _creatorService.GetPayoutProfileAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<PayoutProfileResponseDto?>.Success(
            StatusCodes.Status200OK,
            "Payout profile loaded.",
            profile));
    }

    [HttpPut("payout-profile")]
    public async Task<ActionResult<ApiResponse<PayoutProfileResponseDto>>> UpdatePayoutProfile(
        string slug,
        [FromBody] UpdatePayoutProfileRequestDto request,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var profile = await _creatorService.UpdatePayoutProfileAsync(slug, currentUser.Id, request, ct);

        return Ok(ApiResponse<PayoutProfileResponseDto>.Success(
            StatusCodes.Status200OK,
            "Payout profile saved.",
            profile));
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
