using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CreatorPlatform.Shared.Infrastructure.Redis;

/// <summary>Fails the start on a missing or unusable <c>Redis</c> section. Development gets its defaults before this
/// runs (see <see cref="RedisServiceCollectionExtensions"/>), so a missing connection string only fails elsewhere.
/// Messages never echo the connection string: it can hold an access key.</summary>
public sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public const int MinOperationTimeoutMilliseconds = 50;
    public const int MaxOperationTimeoutMilliseconds = 10_000;
    public const int MinCircuitBreakSeconds = 1;
    public const int MaxCircuitBreakSeconds = 600;
    public const int MaxKeyPrefixLength = 64;

    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            failures.Add("Redis:ConnectionString is required outside Development.");
        else if (!IsParsable(options.ConnectionString))
            failures.Add("Redis:ConnectionString is not a valid StackExchange.Redis configuration string.");

        if (string.IsNullOrEmpty(options.KeyPrefix))
            failures.Add("Redis:KeyPrefix is required (e.g. \"luma:prod:\").");
        else if (options.KeyPrefix.Length > MaxKeyPrefixLength
                 || !options.KeyPrefix.EndsWith(':')
                 || options.KeyPrefix.Any(char.IsWhiteSpace))
            failures.Add($"Redis:KeyPrefix must end with ':', contain no whitespace and be at most {MaxKeyPrefixLength} characters.");

        if (options.OperationTimeoutMilliseconds is < MinOperationTimeoutMilliseconds or > MaxOperationTimeoutMilliseconds)
            failures.Add($"Redis:OperationTimeoutMilliseconds must be between {MinOperationTimeoutMilliseconds} and {MaxOperationTimeoutMilliseconds}.");

        if (options.CircuitBreakSeconds is < MinCircuitBreakSeconds or > MaxCircuitBreakSeconds)
            failures.Add($"Redis:CircuitBreakSeconds must be between {MinCircuitBreakSeconds} and {MaxCircuitBreakSeconds}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsParsable(string connectionString)
    {
        try
        {
            return ConfigurationOptions.Parse(connectionString).EndPoints.Count > 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
