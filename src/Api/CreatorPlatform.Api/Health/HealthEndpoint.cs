using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CreatorPlatform.Api.Health;

/// <summary><c>GET /health</c>: anonymous, not rate limited, 200 for Healthy and Degraded, 503 for Unhealthy. The body
/// carries statuses only (no exception messages, no host names).</summary>
public static class HealthEndpoint
{
    public const string Path = "/health";

    public const string DatabaseCheck = "database";
    public const string RedisCheck = "redis";

    private static readonly TimeSpan DatabaseTimeout = TimeSpan.FromSeconds(5);

    // A backstop only: the Redis client already bounds the ping by its operation timeout.
    private static readonly TimeSpan RedisTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddPlatformHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseCheck, HealthStatus.Unhealthy, timeout: DatabaseTimeout)
            .AddCheck<RedisHealthCheck>(RedisCheck, HealthStatus.Degraded, timeout: RedisTimeout);

        return services;
    }

    public static IEndpointConventionBuilder MapPlatformHealthChecks(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapHealthChecks(Path, new HealthCheckOptions
            {
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                },
                ResponseWriter = WriteResponseAsync
            })
            .AllowAnonymous();

    private static Task WriteResponseAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(
            new HealthResponse(
                report.Status.ToString(),
                report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())),
            context.RequestAborted);

    private sealed record HealthResponse(string Status, IReadOnlyDictionary<string, string> Checks);
}
