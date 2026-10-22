namespace CreatorPlatform.Api.RateLimiting;

/// <summary>Counts a login attempt for the (client IP, email) pair and says whether it is still within the limit.
/// Counts across API instances in Redis; while Redis is unavailable, counts in process with the same rule.</summary>
public interface ILoginAttemptLimiter
{
    ValueTask<bool> TryAcquireAsync(string ipAddress, string email, CancellationToken cancellationToken);
}
