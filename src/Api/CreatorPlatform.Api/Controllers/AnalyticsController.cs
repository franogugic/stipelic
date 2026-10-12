using CreatorPlatform.Analytics.Application.Series;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsSeriesService _seriesService;
    private readonly ICurrentUserContext _currentUserContext;

    public AnalyticsController(IAnalyticsSeriesService seriesService, ICurrentUserContext currentUserContext)
    {
        _seriesService = seriesService;
        _currentUserContext = currentUserContext;
    }

    /// <summary>A page's or a product's views, emails, sales and revenue for one period, in the creator's timezone,
    /// with the period before. <paramref name="scope"/>: page | product; <paramref name="period"/>: today | 7d | 30d |
    /// all (default 7d).</summary>
    [HttpGet("series")]
    public async Task<ActionResult<ApiResponse<AnalyticsSeriesDto>>> Series(
        string slug,
        [FromQuery] string? scope,
        [FromQuery] Guid id,
        [FromQuery] string? period,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        var series = await _seriesService.GetAsync(slug, user.Id, scope, id, period, ct);

        return Ok(ApiResponse<AnalyticsSeriesDto>.Success(StatusCodes.Status200OK, "Analytics loaded.", series));
    }

    private Auth.Application.Dtos.CurrentUserDto GetVerifiedUser()
    {
        var user = _currentUserContext.User ?? throw new UnauthorizedException("Authentication is required.");
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        return user;
    }
}
