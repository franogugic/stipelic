namespace CreatorPlatform.Shared.Infrastructure.Redis;

/// <summary>The <c>Redis</c> configuration section. Validated on start by <see cref="RedisOptionsValidator"/>.</summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>A StackExchange.Redis configuration string. Required outside Development, where it comes from Key Vault.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Put in front of every key (e.g. <c>luma:dev:</c>), so environments can share one Redis.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>The longest a request waits for one Redis call, connecting included.</summary>
    public int OperationTimeoutMilliseconds { get; set; } = 500;

    /// <summary>How long Redis is skipped after a failed call before one call tries it again.</summary>
    public int CircuitBreakSeconds { get; set; } = 15;

    public TimeSpan OperationTimeout => TimeSpan.FromMilliseconds(OperationTimeoutMilliseconds);

    public TimeSpan CircuitBreak => TimeSpan.FromSeconds(CircuitBreakSeconds);
}
