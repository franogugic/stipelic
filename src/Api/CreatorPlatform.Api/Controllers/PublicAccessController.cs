using CreatorPlatform.Access.Application.Dtos;
using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

/// <summary>The public download page (<c>/d/{token}</c>): personal links from order and lead-magnet emails. Anonymous;
/// the token is the secret, so both endpoints share a per-IP limit and nothing is cached.</summary>
[ApiController]
[Route("api/public/access")]
[EnableRateLimiting(RateLimitPolicies.ProductAccess)]
public sealed class PublicAccessController : ControllerBase
{
    private readonly IProductAccessService _productAccessService;

    public PublicAccessController(IProductAccessService productAccessService)
    {
        _productAccessService = productAccessService;
    }

    /// <summary>What the link gives. 404 <c>ACCESS_INVALID</c>, 410 <c>ACCESS_REVOKED</c> (refunded).</summary>
    [HttpGet("{token}")]
    public async Task<ActionResult<ApiResponse<ProductAccessDto>>> Get(string token, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var access = await _productAccessService.GetAsync(token, ct);

        return Ok(ApiResponse<ProductAccessDto>.Success(StatusCodes.Status200OK, "Download link loaded.", access));
    }

    /// <summary>A signed URL for the file, valid for a few minutes; counts as a download.</summary>
    [HttpPost("{token}/download")]
    public async Task<ActionResult<ApiResponse<ProductDownloadDto>>> Download(string token, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var download = await _productAccessService.DownloadAsync(token, ct);

        return Ok(ApiResponse<ProductDownloadDto>.Success(StatusCodes.Status200OK, "Download ready.", download));
    }
}
