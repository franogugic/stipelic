using System.Net;
using System.Text.Json;
using CreatorPlatform.Api.Health;
using CreatorPlatform.Api.Hosting;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary><c>GET /health</c> on a host wired with the API's own registrations (DbContext, Redis, health checks):
/// Healthy with both up, Degraded (still 200) with Redis down, Unhealthy (503) with the database down. The body
/// carries statuses only.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HealthEndpointTests
{
    private readonly PostgresFixture _postgres;
    private readonly RedisFixture _redis;

    public HealthEndpointTests(PostgresFixture postgres, RedisFixture redis)
    {
        _postgres = postgres;
        _redis = redis;
    }

    private static async Task<WebApplication> StartHostAsync(string databaseConnectionString, RedisOptions redis)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = redis.ConnectionString,
            ["Redis:KeyPrefix"] = redis.KeyPrefix
        });
        builder.Services.AddDbContext<CreatorPlatformDbContext>(options => options
            .UseNpgsql(databaseConnectionString)
            .UseEntityConfigurationsFrom(ApiEntityConfigurations.Assemblies));
        builder.Services.AddRedis(builder.Configuration, builder.Environment);
        builder.Services.AddPlatformHealthChecks();

        var app = builder.Build();
        app.MapPlatformHealthChecks();
        await app.StartAsync();
        return app;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetHealthAsync(WebApplication app)
    {
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(HealthEndpoint.Path);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        return (response.StatusCode, body);
    }

    private static void AssertBody(JsonElement body, string status, string database, string redis)
    {
        Assert.Equal(["status", "checks"], body.EnumerateObject().Select(property => property.Name));
        Assert.Equal(status, body.GetProperty("status").GetString());

        var checks = body.GetProperty("checks");
        Assert.Equal(["database", "redis"], checks.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(database, checks.GetProperty("database").GetString());
        Assert.Equal(redis, checks.GetProperty("redis").GetString());
    }

    [Fact]
    public async Task BothUp_IsHealthy()
    {
        await using var app = await StartHostAsync(_postgres.ConnectionString, _redis.CreateOptions());

        var (status, body) = await GetHealthAsync(app);

        Assert.Equal(HttpStatusCode.OK, status);
        AssertBody(body, "Healthy", database: "Healthy", redis: "Healthy");
    }

    [Fact]
    public async Task RedisUnreachable_IsDegraded_AndStill200()
    {
        await using var app = await StartHostAsync(_postgres.ConnectionString, RedisFixture.CreateUnreachableOptions());

        var (status, body) = await GetHealthAsync(app);
        var (statusWhileOpen, bodyWhileOpen) = await GetHealthAsync(app);

        Assert.Equal(HttpStatusCode.OK, status);
        AssertBody(body, "Degraded", database: "Healthy", redis: "Degraded");
        Assert.Equal(HttpStatusCode.OK, statusWhileOpen);
        AssertBody(bodyWhileOpen, "Degraded", database: "Healthy", redis: "Degraded");
    }

    [Fact]
    public async Task DatabaseUnreachable_IsUnhealthy_503_WithoutDetails()
    {
        var unreachableDatabase =
            $"Host=127.0.0.1;Port={ClosedPort.Find()};Database=none;Username=none;Password=none;Timeout=2";
        await using var app = await StartHostAsync(unreachableDatabase, _redis.CreateOptions());

        var (status, body) = await GetHealthAsync(app);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        AssertBody(body, "Unhealthy", database: "Unhealthy", redis: "Healthy");
        Assert.DoesNotContain("127.0.0.1", body.GetRawText());
    }
}
