using System.Net;
using System.Threading.RateLimiting;
using CreatorPlatform.Api.Health;
using CreatorPlatform.Api.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>The global limiter's partitions: per client IP, with the open pixel and the health endpoint exempt.</summary>
public sealed class GlobalRateLimiterTests
{
    private static HttpContext Request(string path, string ip = "203.0.113.7")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    private static int Leases(PartitionedRateLimiter<HttpContext> limiter, HttpContext context, int attempts)
    {
        var acquired = 0;
        for (var i = 0; i < attempts; i++)
        {
            using var lease = limiter.AttemptAcquire(context);
            if (lease.IsAcquired)
                acquired++;
        }

        return acquired;
    }

    [Theory]
    [InlineData(HealthEndpoint.Path)]
    [InlineData("/HEALTH")]
    [InlineData("/api/public/o/some-token")]
    public void ExemptPaths_AreNeverLimited(string path)
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(RateLimitPolicies.GlobalPartition);

        Assert.Equal(1_000, Leases(limiter, Request(path), 1_000));
    }

    [Fact]
    public void OtherPaths_AreLimitedPerClientIp()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(RateLimitPolicies.GlobalPartition);
        var limit = RateLimitPolicies.Global.PermitLimit;

        Assert.Equal(limit, Leases(limiter, Request("/api/auth/me"), limit + 1));
        Assert.Equal(1, Leases(limiter, Request("/api/auth/me", ip: "198.51.100.9"), 1));
        Assert.Equal(0, Leases(limiter, Request("/healthz"), 1));
    }
}
