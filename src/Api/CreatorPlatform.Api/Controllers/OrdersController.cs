using CreatorPlatform.Api.RateLimiting;
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

    /// <summary>One page of orders, newest first. With <paramref name="period"/> (today | 7d | … | all): only the orders
    /// counted in it (Paid by PaidAt, Refunded by refund time, Pending / Failed by CreatedAt), ordered by that date (cursor
    /// <paramref name="afterCountedAt"/> + <paramref name="afterId"/>), with the total and the per-status counts.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<OrdersPageDto>>> List(
        string slug,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? landingPageId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? period,
        [FromQuery] DateTimeOffset? afterCreatedAt,
        [FromQuery] DateTimeOffset? afterCountedAt,
        [FromQuery] Guid? afterId,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        var user = _currentUserContext.User
            ?? throw new UnauthorizedException("Authentication is required.");

        var orders = await _orderService.ListAsync(
            slug, user.Id, new OrderListQuery(productId, landingPageId, status, search, NullIfBlank(period)),
            afterCreatedAt, afterCountedAt, afterId, limit, ct);

        return Ok(ApiResponse<OrdersPageDto>.Success(
            StatusCodes.Status200OK,
            "Orders loaded.",
            orders));
    }

    /// <summary>CSV download of the orders (same filters as the list, incl. <paramref name="search"/> and
    /// <paramref name="period"/>, in the list's order), streamed batch by batch.</summary>
    [HttpGet("export")]
    [EnableRateLimiting(RateLimitPolicies.ExportOrders)]
    public async Task Export(
        string slug,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? landingPageId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? period,
        CancellationToken ct)
    {
        var user = _currentUserContext.User
            ?? throw new UnauthorizedException("Authentication is required.");

        // Validation and ownership are checked here, so 400/401/404 still answer as JSON before any CSV.
        var export = await _orderService.StartExportAsync(
            slug, user.Id, new OrderListQuery(productId, landingPageId, status, search, NullIfBlank(period)), ct);

        await CsvResponseWriter.WriteAsync(
            Response,
            OrdersCsv.FileName(export.CreatorSlug, DateTimeOffset.UtcNow),
            OrdersCsv.Header,
            export.Orders.Select(OrdersCsv.Row),
            ct);
    }

    /// <summary>Without <paramref name="period"/>: the all-time totals and 12-month trend (<see cref="OrderSummaryDto"/>).
    /// With <paramref name="period"/> (today | 7d | 15d | 30d | 2m | 3m | 6m | all): the totals of that period and its
    /// chart buckets (<see cref="OrderPeriodSummaryDto"/>).</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        string slug,
        [FromQuery] string? period,
        [FromQuery] Guid? productId,
        CancellationToken ct)
    {
        var user = _currentUserContext.User
            ?? throw new UnauthorizedException("Authentication is required.");

        if (productId is not null && string.IsNullOrWhiteSpace(period))
            throw new BadRequestException("The product filter needs a period.");

        if (!string.IsNullOrWhiteSpace(period))
        {
            var periodSummary = await _orderService.GetPeriodSummaryAsync(slug, user.Id, period, ct, productId);

            return Ok(ApiResponse<OrderPeriodSummaryDto>.Success(
                StatusCodes.Status200OK,
                "Order summary loaded.",
                periodSummary));
        }

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

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
