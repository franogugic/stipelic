using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICurrentUserContext _currentUserContext;

    public OrdersController(
        IOrderService orderService,
        ICurrentUserContext currentUserContext)
    {
        _orderService = orderService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<OrdersPageDto>>> List(
        string slug,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? landingPageId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? afterCreatedAt,
        [FromQuery] Guid? afterId,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        var user = _currentUserContext.User
            ?? throw new UnauthorizedException("Authentication is required.");

        var orders = await _orderService.ListAsync(
            slug, user.Id, productId, landingPageId, status, search, afterCreatedAt, afterId, limit, ct);

        return Ok(ApiResponse<OrdersPageDto>.Success(
            StatusCodes.Status200OK,
            "Orders loaded.",
            orders));
    }

    /// <summary>CSV download of the orders (same filters as the list, incl. <paramref name="search"/>),
    /// streamed batch by batch.</summary>
    [HttpGet("export")]
    [EnableRateLimiting("ExportOrders")]
    public async Task Export(
        string slug,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? landingPageId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var user = _currentUserContext.User
            ?? throw new UnauthorizedException("Authentication is required.");

        // Validation and ownership are checked here, so 400/401/404 still answer as JSON before any CSV.
        var export = await _orderService.StartExportAsync(slug, user.Id, productId, landingPageId, status, search, ct);

        await CsvResponseWriter.WriteAsync(
            Response,
            OrdersCsv.FileName(export.CreatorSlug, DateTimeOffset.UtcNow),
            OrdersCsv.Header,
            export.Orders.Select(OrdersCsv.Row),
            ct);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<OrderSummaryDto>>> Summary(
        string slug,
        CancellationToken ct)
    {
        var user = _currentUserContext.User
            ?? throw new UnauthorizedException("Authentication is required.");

        var summary = await _orderService.GetSummaryAsync(slug, user.Id, ct);

        return Ok(ApiResponse<OrderSummaryDto>.Success(
            StatusCodes.Status200OK,
            "Order summary loaded.",
            summary));
    }

    [HttpGet("home-summary")]
    public async Task<ActionResult<ApiResponse<HomeSummaryDto>>> HomeSummary(
        string slug,
        CancellationToken ct)
    {
        var user = _currentUserContext.User
            ?? throw new UnauthorizedException("Authentication is required.");

        var summary = await _orderService.GetHomeSummaryAsync(slug, user.Id, ct);

        return Ok(ApiResponse<HomeSummaryDto>.Success(
            StatusCodes.Status200OK,
            "Home summary loaded.",
            summary));
    }
}
