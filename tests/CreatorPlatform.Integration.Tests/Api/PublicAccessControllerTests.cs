using System.Reflection;
using System.Text.Json;
using CreatorPlatform.Access.Application.Dtos;
using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Access.Application.Services;
using CreatorPlatform.Api.Controllers;
using CreatorPlatform.Api.Middlewares;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>The public download page's endpoints: never cached, limited per IP by <c>ProductAccess</c>, and an unknown or
/// refunded link answers 404 <c>ACCESS_INVALID</c> / 410 <c>ACCESS_REVOKED</c>.</summary>
public sealed class PublicAccessControllerTests
{
    private sealed class StubAccess : IProductAccessService
    {
        public Task<ProductAccessDto> GetAsync(string? token, CancellationToken ct) => Task.FromResult(
            new ProductAccessDto("Guide", "Guide.pdf", 1024, new ProductAccessBrandDto("Acme", null, null), "capture", null, null));

        public Task<ProductDownloadDto> DownloadAsync(string? token, CancellationToken ct) =>
            Task.FromResult(new ProductDownloadDto("https://test.blob.core.windows.net/product-files/x?sig=1"));
    }

    private static PublicAccessController Controller() => new(new StubAccess())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task BothEndpoints_AreNeverCached()
    {
        var get = Controller();
        await get.Get("token", CancellationToken.None);
        var download = Controller();
        await download.Download("token", CancellationToken.None);

        Assert.Equal("no-store", get.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-store", download.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void TheController_IsLimitedByTheProductAccessPolicy_30PerTenMinutes()
    {
        var attribute = typeof(PublicAccessController).GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.Equal(RateLimitPolicies.ProductAccess, attribute!.PolicyName);
        var policy = Assert.Single(RateLimitPolicies.Catalog, p => p.Name == RateLimitPolicies.ProductAccess);
        Assert.Equal((30, TimeSpan.FromMinutes(10)), (policy.PermitLimit, policy.Window));
    }

    [Theory]
    [InlineData(404, ProductAccessService.InvalidCode)]
    [InlineData(410, ProductAccessService.RevokedCode)]
    public async Task AnUnusableLink_AnswersItsStatusAndCode(int status, string code)
    {
        Exception exception = status == 404 ? new NotFoundException("Not valid.", code) : new GoneException("Turned off.", code);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await new GlobalExceptionMiddleware(_ => throw exception, NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(context);

        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ARefundedLink_Answers410_WithWhatTheTurnedOffPageShows_AtTheTopLevel()
    {
        var revoked = new ProductAccessRevokedDto(
            "Adriatic Presets", new ProductAccessBrandDto("MH Studio", "#E2553A", null), "A1B2C3D4",
            new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await new GlobalExceptionMiddleware(
            _ => throw new GoneException("Turned off.", ProductAccessService.RevokedCode, revoked),
            NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(context);

        Assert.Equal(410, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = (await JsonDocument.ParseAsync(context.Response.Body)).RootElement;
        Assert.Equal(ProductAccessService.RevokedCode, body.GetProperty("code").GetString());
        Assert.Equal("Turned off.", body.GetProperty("message").GetString());
        Assert.Equal("Adriatic Presets", body.GetProperty("productName").GetString());
        Assert.Equal("MH Studio", body.GetProperty("brand").GetProperty("name").GetString());
        Assert.Equal("#E2553A", body.GetProperty("brand").GetProperty("color").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("brand").GetProperty("logoUrl").ValueKind);
        Assert.Equal("A1B2C3D4", body.GetProperty("orderNumber").GetString());
        Assert.Equal(revoked.RefundedAt, body.GetProperty("refundedAt").GetDateTimeOffset());
        Assert.False(body.TryGetProperty("details", out _));
    }
}
