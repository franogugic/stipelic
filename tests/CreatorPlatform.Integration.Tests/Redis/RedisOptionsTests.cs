using CreatorPlatform.Shared.Infrastructure.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Redis;

/// <summary>The <c>Redis</c> section is checked when the host starts: outside Development a missing connection string
/// stops the start; Development falls back to the local docker-compose Redis. Nothing connects at start.</summary>
public sealed class RedisOptionsTests
{
    private static IHost BuildHost(string environment, Dictionary<string, string?> redisSection)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(
            redisSection.ToDictionary(entry => $"{RedisOptions.SectionName}:{entry.Key}", entry => entry.Value));
        builder.Services.AddLogging(logging => logging.ClearProviders());
        builder.Services.AddRedis(builder.Configuration, builder.Environment);
        return builder.Build();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task OutsideDevelopment_AMissingConnectionString_FailsTheStart(string environment)
    {
        using var host = BuildHost(environment, new() { ["KeyPrefix"] = "luma:prod:" });

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("Redis:ConnectionString is required", exception.Message);
    }

    [Fact]
    public async Task Development_WithoutASection_UsesTheLocalRedis_AndStartsWithoutConnecting()
    {
        using var host = BuildHost(Environments.Development, []);

        await host.StartAsync();

        var options = host.Services.GetRequiredService<IOptions<RedisOptions>>().Value;
        Assert.Equal(RedisServiceCollectionExtensions.DevelopmentConnectionString, options.ConnectionString);
        Assert.Equal(RedisServiceCollectionExtensions.DevelopmentKeyPrefix, options.KeyPrefix);
        Assert.Equal(500, options.OperationTimeoutMilliseconds);
        Assert.Equal(15, options.CircuitBreakSeconds);
        await host.StopAsync();
    }

    [Fact]
    public async Task Development_KeepsConfiguredValues()
    {
        using var host = BuildHost(Environments.Development, new()
        {
            ["ConnectionString"] = "redis.internal:6380,ssl=true,abortConnect=false",
            ["KeyPrefix"] = "luma:local:"
        });

        await host.StartAsync();

        var options = host.Services.GetRequiredService<IOptions<RedisOptions>>().Value;
        Assert.Equal("redis.internal:6380,ssl=true,abortConnect=false", options.ConnectionString);
        Assert.Equal("luma:local:", options.KeyPrefix);
        await host.StopAsync();
    }

    [Theory]
    [InlineData("KeyPrefix", "", "Redis:KeyPrefix is required")]
    [InlineData("KeyPrefix", "luma:prod", "Redis:KeyPrefix must end with ':'")]
    [InlineData("KeyPrefix", "luma prod:", "Redis:KeyPrefix must end with ':'")]
    [InlineData("OperationTimeoutMilliseconds", "0", "Redis:OperationTimeoutMilliseconds")]
    [InlineData("CircuitBreakSeconds", "0", "Redis:CircuitBreakSeconds")]
    [InlineData("ConnectionString", "localhost:6379,notAnOption=1", "not a valid StackExchange.Redis configuration string")]
    public async Task AnInvalidValue_FailsTheStart_WithoutEchoingTheConnectionString(string key, string value, string expected)
    {
        var section = new Dictionary<string, string?>
        {
            ["ConnectionString"] = "secret-host:6380,password=s3cret,abortConnect=false",
            ["KeyPrefix"] = "luma:prod:",
            [key] = value
        };
        using var host = BuildHost(Environments.Production, section);

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(expected, exception.Message);
        Assert.DoesNotContain("s3cret", exception.Message);
        Assert.DoesNotContain("secret-host", exception.Message);
    }

    [Fact]
    public void TheClientSettings_KeepRunningWhileRedisIsDown()
    {
        var configuration = RedisClient.CreateConfiguration(new RedisOptions
        {
            ConnectionString = "localhost:6379",
            KeyPrefix = "luma:test:",
            OperationTimeoutMilliseconds = 750
        });

        Assert.False(configuration.AbortOnConnectFail);
        Assert.True(configuration.ConnectRetry >= 3);
        Assert.Equal(750, configuration.AsyncTimeout);
        Assert.Equal(750, configuration.SyncTimeout);
        Assert.Same(StackExchange.Redis.BacklogPolicy.FailFast, configuration.BacklogPolicy);
    }
}
