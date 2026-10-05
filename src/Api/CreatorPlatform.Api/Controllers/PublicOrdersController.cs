using CreatorPlatform.Api.Responses;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

/// <summary>Anonymous endpoints for buyers on a creator's public pages.</summary>
[ApiController]
[Route("api/public/orders")]
public sealed class PublicOrdersController : ControllerBase
{
    private readonly IOrderReceiptService _orderReceiptService;

    public PublicOrdersController(IOrderReceiptService orderReceiptService)
    {
        _orderReceiptService = orderReceiptService;
    }

    /// <summary>The purchase success page's receipt, by the Stripe Checkout session id from the success URL.
    /// "Pending" until the payment webhook arrives (the page polls); 404 for an unknown or older-than-24-h session.</summary>
    [HttpGet("receipt")]
    [EnableRateLimiting("OrderReceipt")]
    public async Task<ActionResult<ApiResponse<OrderReceiptDto>>> Receipt([FromQuery] string? sessionId, CancellationToken ct)
    {
        var receipt = await _orderReceiptService.GetBySessionIdAsync(sessionId, ct);

        return Ok(ApiResponse<OrderReceiptDto>.Success(StatusCodes.Status200OK, "Receipt loaded.", receipt));
    }
}
