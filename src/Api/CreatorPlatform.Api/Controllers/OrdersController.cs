using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

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
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? afterCreatedAt,
        [FromQuery] Guid? afterId,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        var user = _currentUserContext.User
            ?? throw new UnauthorizedException("Authentication is required.");

        var orders = await _orderService.ListAsync(slug, user.Id, productId, status, afterCreatedAt, afterId, limit, ct);

        return Ok(ApiResponse<OrdersPageDto>.Success(
            StatusCodes.Status200OK,
            "Orders loaded.",
            orders));
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
