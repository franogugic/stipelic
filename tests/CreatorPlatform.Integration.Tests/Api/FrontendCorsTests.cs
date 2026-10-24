using CreatorPlatform.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>S9: the CORS origins come from <c>Cors:AllowedOrigins</c>. Only they get credentialed CORS answers (the
/// anti-forgery header included); localhost is the default only in Development; outside Development the start fails
/// without an origin, or with a plain-http or localhost one.</summary>
public sealed class FrontendCorsTests : IAsyncLifetime
{
    private const string App = "https://app.luma.test";
    private readonly List<WebApplication> _hosts = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var host in _hosts)
            await host.DisposeAsync();
    }

    private async Task<HttpClient> StartAsync(string environment, params string[] origins)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(
            origins.Select((origin, i) => KeyValuePair.Create($"Cors:AllowedOrigins:{i}", (string?)origin)));
        builder.Services.AddFrontendCors(builder.Configuration);

        var app = builder.Build();
        _hosts.Add(app);
        app.UseCors(FrontendCorsOptions.PolicyName);
        app.MapPost("/api/thing", () => "ok");
        await app.StartAsync();
        return app.GetTestClient();
    }

    private static async Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/thing");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-requested-with");
        return await client.SendAsync(request);
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    [Fact]
    public async Task TheConfiguredOrigin_GetsCredentialedCors_WithTheAntiForgeryHeader()
    {
        var client = await StartAsync("Production", App);

        var response = await PreflightAsync(client, App);

        Assert.Equal(App, Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(response, "Access-Control-Allow-Credentials"));
        Assert.Contains("x-requested-with", Header(response, "Access-Control-Allow-Headers")!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://localhost:3000")]
    public async Task AnyOtherOrigin_GetsNoCors(string origin)
    {
        var client = await StartAsync("Production", App);

        Assert.Null(Header(await PreflightAsync(client, origin), "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Development_WithoutConfiguredOrigins_AllowsTheViteDevServers()
    {
        var client = await StartAsync("Development");

        Assert.Equal("http://localhost:3000", Header(await PreflightAsync(client, "http://localhost:3000"), "Access-Control-Allow-Origin"));
        Assert.Null(Header(await PreflightAsync(client, App), "Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData]
    [InlineData("http://app.luma.test")]
    [InlineData("https://localhost:3000")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://app.luma.test/")]
    [InlineData("https://app.luma.test/path")]
    [InlineData("app.luma.test")]
    public async Task OutsideDevelopment_TheStartFails_WithoutAProperOrigin(params string[] origins)
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartAsync("Production", origins));
    }
}
