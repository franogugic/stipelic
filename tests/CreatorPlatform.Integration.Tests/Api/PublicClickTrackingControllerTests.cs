using CreatorPlatform.Api.Controllers;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary><c>GET /api/public/c/{token}</c> always answers a 302 that is never cached and never leaks the referrer:
/// to the stored destination, or to the web app's not-found page.</summary>
public sealed class PublicClickTrackingControllerTests
{
    private sealed class StubClickTracking(Func<string, string?> resolve) : IClickTrackingService
    {
        public Task<string?> ResolveClickAsync(string token, CancellationToken ct) => Task.FromResult(resolve(token));
    }

    private static async Task<(RedirectResult Result, IHeaderDictionary Headers)> GetAsync(Func<string, string?> resolve)
    {
        var controller = new PublicClickTrackingController(
            new StubClickTracking(resolve),
            Options.Create(new MarketingOptions { FrontendBaseUrl = "https://app.luma.test/" }),
            NullLogger<PublicClickTrackingController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Assert.IsType<RedirectResult>(await controller.Get("token", CancellationToken.None));
        return (result, controller.Response.Headers);
    }

    [Fact]
    public async Task AValidLink_RedirectsToItsDestination_WithoutCachingOrReferrer()
    {
        var (result, headers) = await GetAsync(_ => "https://shop.test/buy");

        Assert.Equal("https://shop.test/buy", result.Url);
        Assert.False(result.Permanent);
        Assert.Equal("no-store", headers.CacheControl.ToString());
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
    }

    [Fact]
    public async Task AnInvalidLink_RedirectsToTheNotFoundPage()
    {
        var (result, headers) = await GetAsync(_ => null);

        Assert.Equal("https://app.luma.test/not-found", result.Url);
        Assert.Equal("no-store", headers.CacheControl.ToString());
    }

    [Fact]
    public async Task AFailingLookup_RedirectsToTheNotFoundPage()
    {
        var (result, _) = await GetAsync(_ => throw new InvalidOperationException("database down"));

        Assert.Equal("https://app.luma.test/not-found", result.Url);
    }
}
