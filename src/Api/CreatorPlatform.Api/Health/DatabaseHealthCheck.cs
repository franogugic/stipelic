using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CreatorPlatform.Api.Health;

/// <summary>A <c>SELECT 1</c> through the API's own DbContext. A failure (thrown, or the registration's timeout) is
/// logged by the health check service and reported as the registration's failure status, Unhealthy.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly CreatorPlatformDbContext _dbContext;

    public DatabaseHealthCheck(CreatorPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
