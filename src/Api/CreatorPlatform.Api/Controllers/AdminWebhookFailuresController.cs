using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Api.Webhooks;
using CreatorPlatform.Auth.Application.Authorization;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

/// <summary>Failed Stripe webhooks for the platform admin: the open ones first, a replay of a handler failure once its
/// cause is fixed, and resolving one fixed by hand.</summary>
[ApiController]
[Route("api/admin/webhook-failures")]
public sealed class AdminWebhookFailuresController : ControllerBase
{
    private const string PlatformAdminRole = "platform_admin";

    private readonly WebhookFailureAdminService _webhookFailureAdminService;
    private readonly ICurrentUserContext _currentUserContext;

    public AdminWebhookFailuresController(
        WebhookFailureAdminService webhookFailureAdminService,
        ICurrentUserContext currentUserContext)
    {
        _webhookFailureAdminService = webhookFailureAdminService;
        _currentUserContext = currentUserContext;
    }

    /// <param name="status">open (default), resolved or all.</param>
    /// <param name="limit">1–200, default 50.</param>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<WebhookFailureDto>>>> List(
        [FromQuery] string? status,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        _ = GetPlatformAdmin();

        var failures = await _webhookFailureAdminService.ListAsync(status, limit, ct);

        return Ok(ApiResponse<List<WebhookFailureDto>>.Success(
            StatusCodes.Status200OK,
            "Webhook failures loaded.",
            failures));
    }

    [HttpPost("{id:int}/replay")]
    [EnableRateLimiting(RateLimitPolicies.AdminWebhookFailures)]
    public async Task<ActionResult<ApiResponse<WebhookFailureDto>>> Replay(int id, CancellationToken ct)
    {
        _ = GetPlatformAdmin();

        var failure = await _webhookFailureAdminService.ReplayAsync(id, ct);

        return Ok(ApiResponse<WebhookFailureDto>.Success(
            StatusCodes.Status200OK,
            "Webhook replayed.",
            failure));
    }

    [HttpPost("{id:int}/resolve")]
    [EnableRateLimiting(RateLimitPolicies.AdminWebhookFailures)]
    public async Task<ActionResult<ApiResponse<WebhookFailureDto>>> Resolve(int id, CancellationToken ct)
    {
        _ = GetPlatformAdmin();

        var failure = await _webhookFailureAdminService.ResolveAsync(id, ct);

        return Ok(ApiResponse<WebhookFailureDto>.Success(
            StatusCodes.Status200OK,
            "Webhook failure resolved.",
            failure));
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
