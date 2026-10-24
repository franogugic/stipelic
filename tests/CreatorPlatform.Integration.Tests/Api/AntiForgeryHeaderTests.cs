using System.Net;
using System.Text.Json;
using CreatorPlatform.Api.Security;
using CreatorPlatform.Auth.Application.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>S1: a request that can change data needs <c>X-Requested-With: luma</c> (403 <c>ANTI_FORGERY_HEADER_MISSING</c>
/// without it), reads never do, Stripe's webhooks and the public endpoints are exempt; the session cookie defaults to
/// SameSite=Lax.</summary>
public sealed class AntiForgeryHeaderTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        _app = builder.Build();
        _app.UseMiddleware<AntiForgeryHeaderMiddleware>();
        _app.Map("/{**path}", (HttpContext context) => Results.Text($"reached {context.Request.Method}"));
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

    private Task<HttpResponseMessage> SendAsync(string method, string path, string? header = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (header is not null)
            request.Headers.Add("X-Requested-With", header);
        return _client.SendAsync(request);
    }

    [Theory]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("DELETE", "/api/creators/current")]
    [InlineData("PUT", "/api/creators/acme/settings")]
    [InlineData("PATCH", "/api/creators/acme/products/1")]
    [InlineData("POST", "/api/publicity")]
    [InlineData("POST", "/webhooks/stripey")]
    public async Task AChangeWithoutTheHeader_Is403_AndNeverReachesTheEndpoint(string method, string path)
    {
        var response = await SendAsync(method, path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(AntiForgeryHeaderMiddleware.ErrorCode, body.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("XMLHttpRequest")]
    [InlineData("LUMA")]
    [InlineData("")]
    public async Task AnotherHeaderValue_Is403(string value)
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync("DELETE", "/api/creators/current", value)).StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/auth/logout", "luma")]
    [InlineData("DELETE", "/api/creators/current", "luma")]
    [InlineData("GET", "/api/creators/current", null)]
    [InlineData("HEAD", "/api/creators/current", null)]
    [InlineData("OPTIONS", "/api/creators/current", null)]
    [InlineData("POST", "/webhooks/stripe", null)]
    [InlineData("POST", "/webhooks/stripe/connect", null)]
    [InlineData("POST", "/api/public/unsubscribe/abc", null)]
    [InlineData("POST", "/api/public/landing-pages/acme/guide/captures", null)]
    [InlineData("POST", "/API/Public/landing-pages/acme/guide/checkout", null)]
    public async Task WithTheHeader_AReadOrAnExemptPath_ReachesTheEndpoint(string method, string path, string? header)
    {
        var response = await SendAsync(method, path, header);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void TheSessionCookie_DefaultsToSameSiteLax()
    {
        Assert.Equal("Lax", new AuthOptions().SessionCookieSameSite);
    }
}
