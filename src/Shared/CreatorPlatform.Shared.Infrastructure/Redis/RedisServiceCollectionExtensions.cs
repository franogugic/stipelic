using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Shared.Infrastructure.Redis;

public static class RedisServiceCollectionExtensions
{
    /// <summary>The local docker-compose Redis. Used only in Development, and only when the section leaves it empty.</summary>
    public const string DevelopmentConnectionString = "localhost:6379,abortConnect=false";

    public const string DevelopmentKeyPrefix = "luma:dev:";

    /// <summary>
    /// Binds and validates the <c>Redis</c> section (the start fails on an invalid one) and registers the
    /// process-wide <see cref="IRedisAvailability"/> and <see cref="IRedisClient"/>. Nothing connects here: the first
    /// call does, so the API starts while Redis is down.
    /// </summary>
    public static IServiceCollection AddRedis(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = services
            .AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName));

        // appsettings.Development.json is not in the repository (it holds secrets), so the local defaults live here.
        if (environment.IsDevelopment())
        {
            options.PostConfigure(redis =>
            {
                if (string.IsNullOrWhiteSpace(redis.ConnectionString))
                    redis.ConnectionString = DevelopmentConnectionString;
                if (string.IsNullOrEmpty(redis.KeyPrefix))
                    redis.KeyPrefix = DevelopmentKeyPrefix;
            });
        }

        options.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRedisAvailability, RedisAvailability>();
        services.TryAddSingleton<IRedisClient, RedisClient>();

        return services;
    }
}
