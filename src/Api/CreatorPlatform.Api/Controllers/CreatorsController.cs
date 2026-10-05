using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators")]
public sealed class CreatorsController : ControllerBase
{
    private readonly ICreatorService _creatorService;
    private readonly ICreatorConnectService _creatorConnectService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IOrderService _orderService;
    private readonly IHomeSummaryCache _homeSummaryCache;
    private readonly IViewsSummaryCache _viewsSummaryCache;

    public CreatorsController(
        ICreatorService creatorService,
        ICreatorConnectService creatorConnectService,
        ICurrentUserContext currentUserContext,
        IOrderService orderService,
        IHomeSummaryCache homeSummaryCache,
        IViewsSummaryCache viewsSummaryCache)
    {
        _creatorService = creatorService;
        _creatorConnectService = creatorConnectService;
        _currentUserContext = currentUserContext;
        _orderService = orderService;
        _homeSummaryCache = homeSummaryCache;
        _viewsSummaryCache = viewsSummaryCache;
    }

    [HttpGet("current")]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<CreatorResponseDto?>>> Me(CancellationToken ct)
    {
        var currentUser = GetAuthenticatedUser();

        var response = await _creatorService.GetCurrentForOwnerAsync(currentUser.Id, ct);

        return Ok(ApiResponse<CreatorResponseDto?>.Success(
            StatusCodes.Status200OK,
            "Creator loaded.",
            response));
    }

    [HttpGet("payout-countries")]
    public ActionResult<ApiResponse<List<PayoutCountryDto>>> PayoutCountries()
    {
        _ = GetAuthenticatedUser();

        var countries = _creatorService.GetPayoutCountries();

        return Ok(ApiResponse<List<PayoutCountryDto>>.Success(
            StatusCodes.Status200OK,
            "Payout countries loaded.",
            countries));
    }

    [HttpGet("{slug}/settings")]
    public async Task<ActionResult<ApiResponse<CreatorSettingsResponseDto>>> Settings(
        string slug,
        CancellationToken ct)
    {
        var currentUser = GetAuthenticatedUser();

        var response = await _creatorService.GetSettingsAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<CreatorSettingsResponseDto>.Success(
            StatusCodes.Status200OK,
            "Creator settings loaded.",
            response));
    }

    [HttpPut("{slug}/settings")]
    [EnableRateLimiting("UpdateCreatorSettings")]
    public async Task<ActionResult<ApiResponse<CreatorSettingsResponseDto>>> UpdateSettings(
        string slug,
        [FromBody] UpdateCreatorSettingsRequestDto request,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var response = await _creatorService.UpdateSettingsAsync(slug, currentUser.Id, request, ct);

        return Ok(ApiResponse<CreatorSettingsResponseDto>.Success(
            StatusCodes.Status200OK,
            "Creator settings updated.",
            response));
    }

    [HttpPost]
    [EnableRateLimiting("CreateCreator")]
    public async Task<ActionResult<ApiResponse<CreateCreatorResponseDto>>> Create(
        [FromBody] CreateCreatorRequestDto request,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var response = await _creatorService.CreateAsync(currentUser.Id, request, ct);

        var apiResponse = ApiResponse<CreateCreatorResponseDto>.Success(
            StatusCodes.Status201Created,
            "Creator created.",
            response);

        return Created("/api/creators/current", apiResponse);
    }

    [HttpDelete("current")]
    [EnableRateLimiting("DeleteWorkspace")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCurrent(CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var creatorId = await _creatorService.DeleteCurrentAsync(currentUser.Id, ct);

        // A disabled workspace already fails every ownership check, so nothing can read these entries any more;
        // drop the longer-lived ones anyway rather than leave the data in memory for minutes.
        _homeSummaryCache.Remove(creatorId);
        _viewsSummaryCache.Remove(creatorId);

        return Ok(ApiResponse<object>.Success(
            StatusCodes.Status200OK,
            "Creator disabled.",
            null));
    }

    [HttpPost("current/billing-portal")]
    public async Task<ActionResult<ApiResponse<string>>> BillingPortal(CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var url = await _creatorService.GetBillingPortalUrlAsync(currentUser.Id, ct);

        return Ok(ApiResponse<string>.Success(
            StatusCodes.Status200OK,
            "Billing portal session created.",
            url));
    }

    [HttpDelete("current/subscription")]
    public async Task<ActionResult<ApiResponse<object>>> CancelSubscription(CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        await _creatorService.CancelSubscriptionAsync(currentUser.Id, ct);

        return Ok(ApiResponse<object>.Success(
            StatusCodes.Status200OK,
            "Subscription scheduled for cancellation at the end of the current billing period.",
            null));
    }

    /// <summary>Payment-cancelled screen: leave the unpaid plan and use the workspace on Free.</summary>
    [HttpPost("current/subscription/continue-free")]
    [EnableRateLimiting("ContinueFree")]
    public async Task<ActionResult<ApiResponse<CreatorResponseDto>>> ContinueOnFreePlan(CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var response = await _creatorService.ContinueOnFreePlanAsync(currentUser.Id, ct);

        // The home summary carries plan-dependent numbers (the monthly email limit).
        await _orderService.InvalidateHomeSummaryAsync(response.Slug, currentUser.Id);

        return Ok(ApiResponse<CreatorResponseDto>.Success(
            StatusCodes.Status200OK,
            "Workspace switched to the Free plan.",
            response));
    }

    [HttpPost("current/connect/onboarding-link")]
    [EnableRateLimiting("ConnectOnboarding")]
    public async Task<ActionResult<ApiResponse<ConnectOnboardingLinkResponseDto>>> ConnectOnboardingLink(CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var response = await _creatorConnectService.StartConnectOnboardingAsync(currentUser.Id, currentUser.Email, ct);

        return Ok(ApiResponse<ConnectOnboardingLinkResponseDto>.Success(
            StatusCodes.Status200OK,
            "Connect onboarding link created.",
            response));
    }

    [HttpPost("current/subscription/checkout")]
    [EnableRateLimiting("StartCreatorCheckout")]
    public async Task<ActionResult<ApiResponse<StartCreatorSubscriptionCheckoutResponseDto>>> StartSubscriptionCheckout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] StartCreatorSubscriptionCheckoutRequestDto? request,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var response = await _creatorService.StartSubscriptionCheckoutAsync(
            currentUser.Id, currentUser.Email, request?.PlanCode, ct);

        return Ok(ApiResponse<StartCreatorSubscriptionCheckoutResponseDto>.Success(
            StatusCodes.Status200OK,
            "Creator subscription checkout is ready.",
            response));
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
