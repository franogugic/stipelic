using CreatorPlatform.Analytics.Application.Series;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IOrderService _orderService;
    private readonly IAnalyticsSeriesService _analyticsService;
    private readonly ICurrentUserContext _currentUserContext;

    public ProductsController(
        IProductService productService,
        IOrderService orderService,
        IAnalyticsSeriesService analyticsService,
        ICurrentUserContext currentUserContext)
    {
        _productService = productService;
        _orderService = orderService;
        _analyticsService = analyticsService;
        _currentUserContext = currentUserContext;
    }

    /// <summary>The product's newest sign-ups across the lead pages that give it away, one per email
    /// (<paramref name="limit"/> 1–100, default 5), with how many people signed up in total.</summary>
    [HttpGet("{productPublicId:guid}/signups")]
    public async Task<ActionResult<ApiResponse<ProductSignupsDto>>> Signups(
        string slug,
        Guid productPublicId,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        var signups = await _analyticsService.GetProductSignupsAsync(slug, user.Id, productPublicId, limit, ct);

        return Ok(ApiResponse<ProductSignupsDto>.Success(StatusCodes.Status200OK, "Sign-ups loaded.", signups));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductResponseDto>>>> List(
        string slug,
        [FromQuery] bool includeArchived,
        CancellationToken ct)
    {
        var user = GetAuthenticatedUser();

        var products = await _productService.ListAsync(slug, user.Id, includeArchived, ct);

        return Ok(ApiResponse<List<ProductResponseDto>>.Success(
            StatusCodes.Status200OK,
            "Products loaded.",
            products));
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.CreateProduct)]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> Create(
        string slug,
        [FromBody] CreateProductRequestDto request,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        var product = await _productService.CreateAsync(slug, user.Id, request, ct);

        // Product count on the home summary changed — invalidate so the dashboard reflects it immediately.
        await _orderService.InvalidateHomeSummaryAsync(slug, user.Id);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductResponseDto>.Success(
            StatusCodes.Status201Created,
            "Product created.",
            product));
    }

    [HttpPut("{productId:guid}")]
    [EnableRateLimiting(RateLimitPolicies.UpdateProduct)]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> Update(
        string slug,
        Guid productId,
        [FromBody] UpdateProductRequestDto request,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        var product = await _productService.UpdateAsync(slug, productId, user.Id, request, ct);

        return Ok(ApiResponse<ProductResponseDto>.Success(
            StatusCodes.Status200OK,
            "Product updated.",
            product));
    }

    [HttpDelete("{productId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Archive(
        string slug,
        Guid productId,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        await _productService.ArchiveAsync(slug, productId, user.Id, ct);

        await _orderService.InvalidateHomeSummaryAsync(slug, user.Id);

        return Ok(ApiResponse<object>.Success(
            StatusCodes.Status200OK,
            "Product archived.",
            null));
    }

    [HttpPost("{productId:guid}/restore")]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> Restore(
        string slug,
        Guid productId,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        var product = await _productService.RestoreAsync(slug, productId, user.Id, ct);

        await _orderService.InvalidateHomeSummaryAsync(slug, user.Id);

        return Ok(ApiResponse<ProductResponseDto>.Success(
            StatusCodes.Status200OK,
            "Product restored.",
            product));
    }

    private Auth.Application.Dtos.CurrentUserDto GetAuthenticatedUser()
    {
        var user = _currentUserContext.User;
        if (user is null)
            throw new UnauthorizedException("Authentication is required.");
        return user;
    }

    private Auth.Application.Dtos.CurrentUserDto GetVerifiedUser()
    {
        var user = GetAuthenticatedUser();
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        return user;
    }
}
