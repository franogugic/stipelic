using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Authorization;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/admin/payouts")]
public sealed class AdminPayoutsController : ControllerBase
{
    private const string PlatformAdminRole = "platform_admin";

    private readonly IPayoutAdminService _payoutAdminService;
    private readonly ICurrentUserContext _currentUserContext;

    public AdminPayoutsController(
        IPayoutAdminService payoutAdminService,
        ICurrentUserContext currentUserContext)
    {
        _payoutAdminService = payoutAdminService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<AdminPayoutQueueItemDto>>>> List(
        [FromQuery] string? status,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        _ = GetPlatformAdmin();

        var effectiveLimit = limit <= 0 ? 50 : limit;
        var queue = await _payoutAdminService.ListQueueAsync(status, effectiveLimit, ct);

        return Ok(ApiResponse<List<AdminPayoutQueueItemDto>>.Success(
            StatusCodes.Status200OK,
            "Payout queue loaded.",
            queue));
    }

    [HttpGet("balances")]
    public async Task<ActionResult<ApiResponse<List<CreatorBalanceSummaryDto>>>> Balances(
        [FromQuery] int? minCents,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        _ = GetPlatformAdmin();

        var effectiveLimit = limit <= 0 ? 100 : limit;
        var balances = await _payoutAdminService.GetBalancesAsync(minCents, effectiveLimit, ct);

        return Ok(ApiResponse<List<CreatorBalanceSummaryDto>>.Success(
            StatusCodes.Status200OK,
            "Payout balances loaded.",
            balances));
    }

    [HttpPost]
    [EnableRateLimiting("AdminPayouts")]
    public async Task<ActionResult<ApiResponse<PayoutDto>>> Create(
        [FromBody] CreatePayoutRequestDto request,
        CancellationToken ct)
    {
        _ = GetPlatformAdmin();

        var payout = await _payoutAdminService.CreatePayoutAsync(request, ct);

        var apiResponse = ApiResponse<PayoutDto>.Success(
            StatusCodes.Status201Created,
            "Payout created.",
            payout);

        return Created($"/api/admin/payouts/{payout.PublicId}", apiResponse);
    }

    [HttpPost("{publicId}/mark-paid")]
    [EnableRateLimiting("AdminPayouts")]
    public async Task<ActionResult<ApiResponse<PayoutDto>>> MarkPaid(
        Guid publicId,
        [FromBody] MarkPayoutPaidRequestDto request,
        CancellationToken ct)
    {
        _ = GetPlatformAdmin();

        var payout = await _payoutAdminService.MarkPaidAsync(publicId, request, ct);

        return Ok(ApiResponse<PayoutDto>.Success(
            StatusCodes.Status200OK,
            "Payout marked as paid.",
            payout));
    }

    [HttpPost("{publicId}/mark-failed")]
    [EnableRateLimiting("AdminPayouts")]
    public async Task<ActionResult<ApiResponse<PayoutDto>>> MarkFailed(
        Guid publicId,
        [FromBody] MarkPayoutFailedRequestDto request,
        CancellationToken ct)
    {
        _ = GetPlatformAdmin();

        var payout = await _payoutAdminService.MarkFailedAsync(publicId, request, ct);

        return Ok(ApiResponse<PayoutDto>.Success(
            StatusCodes.Status200OK,
            "Payout marked as failed.",
            payout));
    }

    /// <summary>Returns the authenticated user or throws 401.</summary>
    private CurrentUserDto GetAuthenticatedUser()
    {
        var user = _currentUserContext.User;
        if (user is null)
            throw new UnauthorizedException("Authentication is required.");
        return user;
    }

    /// <summary>Returns the authenticated, email-verified, platform_admin user or throws 401/403.</summary>
    private CurrentUserDto GetPlatformAdmin()
    {
        var user = GetAuthenticatedUser();
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        RoleGuard.RequireRole(user, PlatformAdminRole);
        return user;
    }
}
