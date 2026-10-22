using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>
/// One throwaway Redis 7.4 per collection. Tests isolate their keys with their own key prefix
/// (<see cref="UniqueKeyPrefix"/>) instead of flushing, so they never depend on each other.
/// "Redis down" is a client pointed at a closed local port (<see cref="CreateUnreachableOptions"/>): it fails at once,
/// and unlike stopping or pausing the container it cannot break the other tests sharing it.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7.4-alpine").Build();
    private ConnectionMultiplexer? _multiplexer;

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>A raw connection (no key prefix, no circuit breaker) for arranging and asserting on keys.</summary>
    public IConnectionMultiplexer Multiplexer =>
        _multiplexer ?? throw new InvalidOperationException("The Redis fixture is not initialized.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_multiplexer is not null)
            await _multiplexer.DisposeAsync();

        await _container.DisposeAsync();
    }

    public static string UniqueKeyPrefix() => $"test:{Guid.NewGuid():N}:";

    public RedisOptions CreateOptions(string? keyPrefix = null) => new()
    {
        ConnectionString = $"{ConnectionString},abortConnect=false",
        KeyPrefix = keyPrefix ?? UniqueKeyPrefix()
    };

    public static RedisOptions CreateUnreachableOptions() => new()
    {
        ConnectionString = $"127.0.0.1:{ClosedPort.Find()},abortConnect=false",
        KeyPrefix = UniqueKeyPrefix()
    };

    /// <summary>A client the way the API builds one (its own connection and circuit breaker), standing in for one API
    /// instance; dispose it at the end of the test.</summary>
    public static TestRedisClient CreateClient(RedisOptions options, TimeProvider? timeProvider = null)
    {
        var time = timeProvider ?? TimeProvider.System;
        var availabilityLog = new ListLogger<RedisAvailability>();
        var availability = new RedisAvailability(Options.Create(options), time, availabilityLog);
        var client = new RedisClient(Options.Create(options), availability, time, new ListLogger<RedisClient>());
        return new TestRedisClient(client, availability, availabilityLog);
    }
}

public sealed record TestRedisClient(
    RedisClient Client,
    RedisAvailability Availability,
    ListLogger<RedisAvailability> AvailabilityLog) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Client.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>
{
    public const string Name = "Redis";
}
