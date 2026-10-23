namespace CreatorPlatform.Api.RateLimiting;

/// <summary>Login attempts allowed per client IP and email address: <see cref="PermitLimit"/> per fixed
/// <see cref="Window"/>, counted before the password check (successful logins count too).</summary>
public sealed class LoginAttemptLimiterOptions
{
    public int PermitLimit { get; set; } = 5;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>The most (IP, email) windows the in-process fallback tracks at once; this bounds its memory.</summary>
    public int MaxTrackedPartitions { get; set; } = 100_000;
}
