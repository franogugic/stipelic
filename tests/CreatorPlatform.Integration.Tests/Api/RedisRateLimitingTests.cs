using System.Net;
using System.Text.Json;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>
/// The API's rate limiter (<see cref="RateLimiterServiceCollectionExtensions.AddPlatformRateLimiter"/>) on real
/// TestServer hosts sharing a real Redis, standing in for API instances: limits count across hosts, answer 429 with the
/// JSON body and Retry-After, reset with a new window, still apply (per host) with Redis unreachable, and leave the open
/// pixel and /health alone.
/// </summary>
[Collection(RedisCollection.Name)]
public sealed class RedisRateLimitingTests : IAsyncLifetime
{
    private const string ClientIpHeader = "X-Test-Client-Ip";
    private static readonly RateLimitPolicyDefinition TestPolicy = new("Test", 3, TimeSpan.FromMinutes(1));
    private static readonly RateLimitPolicyDefinition RoomyGlobal = new("Global", 1_000, TimeSpan.FromMinutes(1));

    private readonly RedisFixture _redis;
    private readonly string _prefix = RedisFixture.UniqueKeyPrefix();
    private readonly List<WebApplication> _hosts = [];

    public RedisRateLimitingTests(RedisFixture redis)
    {
        _redis = redis;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var host in _hosts)
            await host.DisposeAsync();
    }

    /// <summary>One API instance: its own Redis connection, circuit breaker and in-process fallback.</summary>
    private async Task<HttpClient> StartHostAsync(
        RedisOptions? redis = null,
        IReadOnlyList<RateLimitPolicyDefinition>? policies = null,
        RateLimitPolicyDefinition? global = null)
    {
        redis ??= _redis.CreateOptions(_prefix);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = redis.ConnectionString,
            ["Redis:KeyPrefix"] = redis.KeyPrefix
        });
        builder.Services.AddRedis(builder.Configuration, builder.Environment);
        builder.Services.AddPlatformRateLimiter(policies ?? [TestPolicy], global ?? RoomyGlobal);

        var app = builder.Build();
        // TestServer requests have no remote address; let a test pick one.
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue(ClientIpHeader, out var ip))
                context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
            return next(context);
        });
        app.UseRateLimiter();
        app.MapGet("/limited", () => "ok").RequireRateLimiting(TestPolicy.Name);
        app.MapGet("/open", () => "ok");
        app.MapGet("/api/public/o/{token}", (string token) => "pixel");
        app.MapGet("/health", () => "healthy");
        foreach (var policy in policies ?? [])
            app.MapGet($"/policy/{policy.Name}", () => "ok").RequireRateLimiting(policy.Name);

        await app.StartAsync();
        _hosts.Add(app);
        return app.GetTestClient();
    }

    private static async Task<HttpStatusCode> GetAsync(HttpClient client, string path, string? clientIp = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (clientIp is not null)
            request.Headers.Add(ClientIpHeader, clientIp);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<List<HttpStatusCode>> GetManyAsync(HttpClient client, string path, int times)
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < times; i++)
            statuses.Add(await GetAsync(client, path));
        return statuses;
    }

    private static async Task AssertRejectedAsync(HttpClient client, string path, TimeSpan window)
    {
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var retryAfter = int.Parse(Assert.Single(response.Headers.GetValues("Retry-After")));
        Assert.InRange(retryAfter, 1, (int)Math.Ceiling(window.TotalSeconds));
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(429, body.GetProperty("statusCode").GetInt32());
        Assert.Equal("RATE_LIMITED", body.GetProperty("code").GetString());
        Assert.Equal("Too many requests. Please try again later.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RequestNPlusOne_Is429_WithTheJsonBodyAndRetryAfter_AndTheCountIsInRedis()
    {
        var client = await StartHostAsync();

        Assert.All(await GetManyAsync(client, "/limited", 3), status => Assert.Equal(HttpStatusCode.OK, status));
        await AssertRejectedAsync(client, "/limited", TestPolicy.Window);

        var database = _redis.Multiplexer.GetDatabase();
        var key = $"{_prefix}rl:Test:unknown";
        Assert.Equal(4, (long)await database.StringGetAsync(key));
        var ttl = await database.KeyTimeToLiveAsync(key);
        Assert.InRange(ttl!.Value, TimeSpan.FromSeconds(50), TestPolicy.Window);
    }

    [Fact]
    public async Task TwoHosts_ShareTheLimit()
    {
        var a = await StartHostAsync();
        var b = await StartHostAsync();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(a, "/limited"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(b, "/limited"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(a, "/limited"));

        await AssertRejectedAsync(b, "/limited", TestPolicy.Window);
        await AssertRejectedAsync(a, "/limited", TestPolicy.Window);
    }

    [Fact]
    public async Task ClientIps_AreCountedSeparately()
    {
        var client = await StartHostAsync();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "/limited", clientIp: "203.0.113.7"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, "/limited", clientIp: "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "/limited", clientIp: "2001:db8::1"));
    }

    [Fact]
    public async Task ANewWindow_ResetsTheCount()
    {
        var shortPolicy = TestPolicy with { Window = TimeSpan.FromSeconds(1) };
        var client = await StartHostAsync(policies: [shortPolicy]);
        await GetManyAsync(client, "/limited", 3);
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, "/limited"));

        await Task.Delay(TimeSpan.FromMilliseconds(1_200));

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "/limited"));
    }

    [Fact]
    public async Task RedisUnreachable_LimitsStillApplyPerHost_AndNothingIs500()
    {
        var a = await StartHostAsync(redis: RedisFixture.CreateUnreachableOptions());
        var b = await StartHostAsync(redis: RedisFixture.CreateUnreachableOptions());

        var onA = await GetManyAsync(a, "/limited", 3);
        await AssertRejectedAsync(a, "/limited", TestPolicy.Window);
        var onB = await GetManyAsync(b, "/limited", 3);

        // Each host counts on its own until Redis is back: B still has its own 3.
        Assert.All(onA.Concat(onB), status => Assert.Equal(HttpStatusCode.OK, status));
        await AssertRejectedAsync(b, "/limited", TestPolicy.Window);
        Assert.Equal(HttpStatusCode.OK, await GetAsync(a, "/open"));
    }

    [Fact]
    public async Task TheGlobalLimiter_CountsAcrossHosts()
    {
        var global = new RateLimitPolicyDefinition("Global", 5, TimeSpan.FromMinutes(1));
        var a = await StartHostAsync(global: global);
        var b = await StartHostAsync(global: global);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add(await GetAsync(a, "/open"));
            statuses.Add(await GetAsync(b, "/open"));
        }

        Assert.Equal(5, statuses.Count(status => status == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        await AssertRejectedAsync(a, "/open", global.Window);
    }

    [Fact]
    public async Task TheOpenPixelAndHealth_AreNotLimited()
    {
        var global = new RateLimitPolicyDefinition("Global", 2, TimeSpan.FromMinutes(1));
        var a = await StartHostAsync(global: global);
        var b = await StartHostAsync(global: global);

        foreach (var client in new[] { a, b })
        {
            Assert.All(await GetManyAsync(client, "/api/public/o/token", 10), status => Assert.Equal(HttpStatusCode.OK, status));
            Assert.All(await GetManyAsync(client, "/health", 10), status => Assert.Equal(HttpStatusCode.OK, status));
        }

        var server = _redis.Multiplexer.GetServers().Single();
        Assert.Empty(server.Keys(pattern: $"{_prefix}*").ToList());
    }

    [Fact]
    public async Task EveryCatalogPolicy_IsRegistered_AndCountedInRedis()
    {
        var client = await StartHostAsync(policies: RateLimitPolicies.Catalog, global: RateLimitPolicies.Global);

        foreach (var policy in RateLimitPolicies.Catalog)
            Assert.Equal(HttpStatusCode.OK, await GetAsync(client, $"/policy/{policy.Name}"));

        var database = _redis.Multiplexer.GetDatabase();
        foreach (var policy in RateLimitPolicies.Catalog)
            Assert.Equal(1, (long)await database.StringGetAsync($"{_prefix}rl:{policy.Name}:unknown"));
        Assert.Equal(RateLimitPolicies.Catalog.Count, (long)await database.StringGetAsync($"{_prefix}rl:Global:unknown"));
    }
}
