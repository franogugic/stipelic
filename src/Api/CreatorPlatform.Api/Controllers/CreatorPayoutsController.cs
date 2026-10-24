using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

/// <summary>The payouts screen. Creators are paid only through their own connected Stripe account, so this is its state.</summary>
[ApiController]
[Route("api/creators/{slug}")]
public sealed class CreatorPayoutsController : ControllerBase
{
    private readonly ICreatorConnectService _creatorConnectService;
    private readonly ICurrentUserContext _currentUserContext;

    public CreatorPayoutsController(
        ICreatorConnectService creatorConnectService,
        ICurrentUserContext currentUserContext)
    {
        _creatorConnectService = creatorConnectService;
        _currentUserContext = currentUserContext;
    }

    /// <summary>Stripe Connect details for the payouts screen: account id, milestone dates and the live payout
    /// schedule (null when Stripe can't be reached).</summary>
    [HttpGet("payouts/connect")]
    public async Task<ActionResult<ApiResponse<ConnectPayoutDetailsResponseDto>>> ConnectDetails(string slug, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var details = await _creatorConnectService.GetPayoutDetailsAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<ConnectPayoutDetailsResponseDto>.Success(
            StatusCodes.Status200OK,
            "Connect payout details loaded.",
            details));
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
