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

    /// <summary>A page's, a product's or the whole workspace's views, emails, sales and revenue for one period, in the
    /// creator's timezone, with the period before. <paramref name="scope"/>: page | product | workspace (no
    /// <paramref name="id"/>); <paramref name="period"/>: today | 7d | 15d | 30d | 2m | 3m | 6m | all (default 7d).</summary>
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

    /// <summary>Paid revenue and sales per product for one period (default 30d): the best 20 products with revenue and
    /// the revenue of the rest.</summary>
    [HttpGet("products")]
    public async Task<ActionResult<ApiResponse<AnalyticsProductBreakdownDto>>> Products(
        string slug,
        [FromQuery] string? period,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        var breakdown = await _seriesService.GetProductBreakdownAsync(slug, user.Id, period, ct);

        return Ok(ApiResponse<AnalyticsProductBreakdownDto>.Success(StatusCodes.Status200OK, "Product revenue loaded.", breakdown));
    }

    private Auth.Application.Dtos.CurrentUserDto GetVerifiedUser()
    {
        var user = _currentUserContext.User ?? throw new UnauthorizedException("Authentication is required.");
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        return user;
    }
}
