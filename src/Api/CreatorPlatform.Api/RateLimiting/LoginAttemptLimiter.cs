using System.Collections.Concurrent;

namespace CreatorPlatform.Api.RateLimiting;

public sealed class LoginAttemptLimiter
{
    private const int PermitLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, LoginAttemptWindow> _windows = new();

    public bool IsAllowed(string ipAddress, string email)
    {
        var partitionKey = $"{ipAddress}:{email.Trim().ToLowerInvariant()}";
        var now = DateTimeOffset.UtcNow;

        var window = _windows.AddOrUpdate(
            partitionKey,
            // id window with that key does not exists
            _ => new LoginAttemptWindow(now.Add(Window), 1),
            (_, existingWindow) =>
            {
                // restart timer
                if (existingWindow.ExpiresAt <= now)
                    return new LoginAttemptWindow(now.Add(Window), 1);

                // increment attempt count
                return existingWindow with
                {
                    AttemptCount = existingWindow.AttemptCount + 1
                };
            });

        return window.AttemptCount <= PermitLimit;
    }

    private sealed record LoginAttemptWindow(DateTimeOffset ExpiresAt, int AttemptCount);
}
