using System.Net;
using CreatorPlatform.Api.Networking;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>
/// The client IP behind a proxy, on a host wired like the API (trusted forwarded headers first, then the Redis rate
/// limiter): X-Forwarded-For is read only from a configured proxy or network, only its last entry counts, and with
/// nothing configured it is never read (not even from loopback).
/// </summary>
[Collection(RedisCollection.Name)]
public sealed class TrustedProxyTests : IAsyncLifetime
{
    // Stands in for the address of whoever opened the connection (TestServer requests have none).
    private const string SenderHeader = "X-Test-Sender-Ip";
    private const string Proxy = "10.0.0.5";
    private static readonly RateLimitPolicyDefinition OnePerMinute = new("Test", 1, TimeSpan.FromMinutes(1));

    private readonly RedisFixture _redis;
    private readonly List<WebApplication> _hosts = [];

    public TrustedProxyTests(RedisFixture redis)
    {
        _redis = redis;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var host in _hosts)
            await host.DisposeAsync();
    }

    private async Task<HttpClient> StartHostAsync(
        string[]? knownProxies = null, string[]? knownNetworks = null, int? forwardLimit = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var redis = _redis.CreateOptions();
        var settings = new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = redis.ConnectionString,
            ["Redis:KeyPrefix"] = redis.KeyPrefix
        };
        for (var i = 0; i < (knownProxies?.Length ?? 0); i++)
            settings[$"ForwardedHeaders:KnownProxies:{i}"] = knownProxies![i];
        for (var i = 0; i < (knownNetworks?.Length ?? 0); i++)
            settings[$"ForwardedHeaders:KnownNetworks:{i}"] = knownNetworks![i];
        if (forwardLimit is not null)
            settings["ForwardedHeaders:ForwardLimit"] = forwardLimit.Value.ToString();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddRedis(builder.Configuration, builder.Environment);
        builder.Services.AddTrustedProxies(builder.Configuration);
        builder.Services.AddPlatformRateLimiter([OnePerMinute], new RateLimitPolicyDefinition("Global", 1_000, TimeSpan.FromMinutes(1)));

        var app = builder.Build();
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue(SenderHeader, out var sender))
                context.Connection.RemoteIpAddress = IPAddress.Parse(sender.ToString());
            return next(context);
        });
        app.UseTrustedForwardedHeaders();
        app.UseRateLimiter();
        app.MapGet("/whoami", (HttpContext context) => $"{ClientIp.From(context)} {context.Request.Scheme}");
        app.MapGet("/limited", () => "ok").RequireRateLimiting(OnePerMinute.Name);

        await app.StartAsync();
        _hosts.Add(app);
        return app.GetTestClient();
    }

    private static HttpRequestMessage Request(string path, string sender, string? forwardedFor = null, string? forwardedProto = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(SenderHeader, sender);
        if (forwardedFor is not null)
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        if (forwardedProto is not null)
            request.Headers.Add("X-Forwarded-Proto", forwardedProto);
        return request;
    }

    private static async Task<string> WhoAmIAsync(HttpClient client, string sender, string? forwardedFor = null, string? forwardedProto = null)
    {
        using var request = Request("/whoami", sender, forwardedFor, forwardedProto);
        using var response = await client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<HttpStatusCode> LimitedAsync(HttpClient client, string sender, string? forwardedFor = null)
    {
        using var request = Request("/limited", sender, forwardedFor);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task BehindAKnownProxy_DifferentForwardedClients_LandInDifferentPartitions()
    {
        var client = await StartHostAsync(knownProxies: [Proxy]);

        Assert.Equal(HttpStatusCode.OK, await LimitedAsync(client, Proxy, forwardedFor: "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await LimitedAsync(client, Proxy, forwardedFor: "198.51.100.9"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await LimitedAsync(client, Proxy, forwardedFor: "203.0.113.7"));
        Assert.Equal("203.0.113.7 https", await WhoAmIAsync(client, Proxy, forwardedFor: "203.0.113.7", forwardedProto: "https"));
    }

    [Fact]
    public async Task AKnownNetwork_IsTrustedLikeAProxy()
    {
        var client = await StartHostAsync(knownNetworks: ["10.0.0.0/23"]);

        Assert.Equal("203.0.113.7 http", await WhoAmIAsync(client, "10.0.1.200", forwardedFor: "203.0.113.7"));
        Assert.Equal("10.0.2.1 http", await WhoAmIAsync(client, "10.0.2.1", forwardedFor: "203.0.113.7"));
    }

    [Fact]
    public async Task FromAnUnknownSender_TheHeadersAreIgnored_AndItIsLimitedByItsOwnAddress()
    {
        var client = await StartHostAsync(knownProxies: [Proxy]);

        Assert.Equal("192.0.2.1 http", await WhoAmIAsync(client, "192.0.2.1", forwardedFor: "203.0.113.7", forwardedProto: "https"));
        Assert.Equal(HttpStatusCode.OK, await LimitedAsync(client, "192.0.2.1", forwardedFor: "203.0.113.7"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await LimitedAsync(client, "192.0.2.1", forwardedFor: "198.51.100.9"));
    }

    [Fact]
    public async Task OnlyTheEntryOurProxyAppended_Counts()
    {
        var client = await StartHostAsync(knownProxies: [Proxy]);

        // The client sent "1.2.3.4" itself; our proxy appended the address it saw.
        Assert.Equal("203.0.113.7 http", await WhoAmIAsync(client, Proxy, forwardedFor: "1.2.3.4, 203.0.113.7"));
    }

    [Fact]
    public async Task WithNothingConfigured_TheHeadersAreNeverRead_NotEvenFromLoopback()
    {
        var client = await StartHostAsync();

        Assert.Equal("127.0.0.1 http", await WhoAmIAsync(client, "127.0.0.1", forwardedFor: "203.0.113.7", forwardedProto: "https"));
        Assert.Equal("::1 http", await WhoAmIAsync(client, "::1", forwardedFor: "203.0.113.7"));
    }

    [Theory]
    [InlineData("KnownProxies", "10.0.0.300", "not an IP address")]
    [InlineData("KnownNetworks", "10.0.0.0/33", "not a CIDR network")]
    [InlineData("KnownNetworks", "10.0.0.5", "not a CIDR network")]
    public void AnUnparsableEntry_FailsValidation(string list, string value, string expected)
    {
        var options = new TrustedProxyOptions();
        (list == "KnownProxies" ? options.KnownProxies : options.KnownNetworks).Add(value);

        var result = new TrustedProxyOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(expected, result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void AForwardLimitOutOfRange_FailsValidation(int forwardLimit)
    {
        var result = new TrustedProxyOptionsValidator().Validate(null, new TrustedProxyOptions { ForwardLimit = forwardLimit });

        Assert.True(result.Failed);
    }

    [Fact]
    public async Task AnInvalidSection_FailsTheStart()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartHostAsync(knownNetworks: ["not-a-network"]));
    }

    [Theory]
    [InlineData(null, "unknown")]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    public void ClientIp_IsNormalized(string? address, string expected)
    {
        Assert.Equal(expected, ClientIp.From(address is null ? null : IPAddress.Parse(address)));
    }
}
