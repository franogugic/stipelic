using System.Globalization;
using System.Threading.RateLimiting;
using CreatorPlatform.Api.Responses;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.RateLimiting;

public static class RateLimiterServiceCollectionExtensions
{
    /// <summary>
    /// The global limiter and one named policy per catalog entry, all counted in Redis across instances (in process
    /// per instance while Redis is unavailable). A policy added to <see cref="RateLimitPolicies.Catalog"/> is picked up
    /// here with no other code. Tests pass their own <paramref name="policies"/> and <paramref name="global"/>.
    /// </summary>
    public static IServiceCollection AddPlatformRateLimiter(
        this IServiceCollection services,
        IReadOnlyList<RateLimitPolicyDefinition>? policies = null,
        RateLimitPolicyDefinition? global = null)
    {
        var globalPolicy = global ?? RateLimitPolicies.Global;

        services.AddSingleton<RedisRateLimiters>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                httpContext => RateLimitPolicies.GlobalPartition(httpContext, globalPolicy));
            options.OnRejected = WriteRejectionAsync;

            foreach (var policy in policies ?? RateLimitPolicies.Catalog)
                options.AddPolicy(policy.Name, httpContext => RateLimitPolicies.Partition(httpContext, policy));
        });

        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken ct)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/json";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        await response.WriteAsJsonAsync(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            Message = "Too many requests. Please try again later.",
            Code = "RATE_LIMITED"
        }, ct);
    }
}
