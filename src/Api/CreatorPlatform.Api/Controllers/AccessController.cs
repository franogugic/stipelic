using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Api.Responses;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AccessController : ControllerBase
{
    private readonly IAccessRedirectService _accessRedirectService;

    public AccessController(IAccessRedirectService accessRedirectService)
    {
        _accessRedirectService = accessRedirectService;
    }

    [HttpGet("{publicId:guid}")]
    public async Task<IActionResult> Redirect(Guid publicId, CancellationToken ct)
    {
        var accessUrl = await _accessRedirectService.GetAccessUrlAsync(publicId, ct);

        if (accessUrl is null)
        {
            return NotFound(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Message = "Order not found or not paid.",
                Code = "ORDER_NOT_FOUND",
            });
        }

        return Redirect(accessUrl);
    }
}
