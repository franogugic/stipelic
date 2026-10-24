using System.Net;
using CreatorPlatform.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>S8: every response — a 404 too — carries nosniff, no-referrer, a locked-down Permissions-Policy and CSP; HTTPS
/// responses carry HSTS outside Development; outside Development AllowedHosts must name real hosts (the start fails on
/// "*" or nothing) and any other Host is refused.</summary>
public sealed class SecurityHeadersTests : IAsyncLifetime
{
    private const string Host = "api.luma.test";
    private readonly List<WebApplication> _hosts = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var host in _hosts)
            await host.DisposeAsync();
    }

    private static WebApplicationBuilder Builder(string environment, string? allowedHosts)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection([KeyValuePair.Create("AllowedHosts", allowedHosts)]);
        return builder;
    }

    private async Task<HttpClient> StartAsync(string environment, string allowedHosts)
    {
        var builder = Builder(environment, allowedHosts);
        builder.Services.AddPlatformSecurity(builder.Configuration, builder.Environment);
        var app = builder.Build();
        _hosts.Add(app);
        app.UsePlatformSecurityHeaders(app.Environment);
        app.MapGet("/api/thing", () => "ok");
        await app.StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri($"https://{Host}");
        return client;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(",", values)
            : null;

    [Theory]
    [InlineData("/api/thing", HttpStatusCode.OK)]
    [InlineData("/api/missing", HttpStatusCode.NotFound)]
    public async Task EveryResponse_CarriesTheSecurityHeaders_AndHstsOutsideDevelopment(string path, HttpStatusCode status)
    {
        var client = await StartAsync("Production", Host);

        var response = await client.GetAsync(path);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal(SecurityHeadersMiddleware.PermissionsPolicy, Header(response, "Permissions-Policy"));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Header(response, "Content-Security-Policy"));
        Assert.Equal("max-age=15552000", Header(response, "Strict-Transport-Security"));
    }

    [Fact]
    public async Task Development_HasTheHeaders_ButNoHsts()
    {
        var client = await StartAsync("Development", "*");

        var response = await client.GetAsync("/api/thing");

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Null(Header(response, "Strict-Transport-Security"));
    }

    [Fact]
    public async Task AnotherHost_IsRefused()
    {
        var client = await StartAsync("Production", Host);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/thing");
        request.Headers.Host = "evil.example";

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("api.luma.test;*")]
    public void OutsideDevelopment_TheStartFails_WithoutRealAllowedHosts(string? allowedHosts)
    {
        var builder = Builder("Production", allowedHosts);

        Assert.Throws<InvalidOperationException>(() => builder.Services.AddPlatformSecurity(builder.Configuration, builder.Environment));
    }
}
