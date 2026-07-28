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
    private readonly IHomeSummaryCache _homeSummaryCache;
    private readonly ICurrentUserContext _currentUserContext;

    public ProductsController(
        IProductService productService,
        IHomeSummaryCache homeSummaryCache,
        ICurrentUserContext currentUserContext)
    {
        _productService = productService;
        _homeSummaryCache = homeSummaryCache;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductResponseDto>>>> List(
        string slug,
        CancellationToken ct)
    {
        var user = GetAuthenticatedUser();

        var products = await _productService.ListAsync(slug, user.Id, ct);

        return Ok(ApiResponse<List<ProductResponseDto>>.Success(
            StatusCodes.Status200OK,
            "Products loaded.",
            products));
    }

    [HttpPost]
    [EnableRateLimiting("CreateProduct")]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> Create(
        string slug,
        [FromBody] CreateProductRequestDto request,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();

        var product = await _productService.CreateAsync(slug, user.Id, request, ct);

        // Product count on the home summary changed — invalidate so the dashboard reflects it immediately.
        _homeSummaryCache.Remove(slug);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ProductResponseDto>.Success(
            StatusCodes.Status201Created,
            "Product created.",
            product));
    }

    [HttpPut("{productId:guid}")]
    [EnableRateLimiting("UpdateProduct")]
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

        _homeSummaryCache.Remove(slug);

        return Ok(ApiResponse<object>.Success(
            StatusCodes.Status200OK,
            "Product archived.",
            null));
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
