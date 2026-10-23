using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Api.Networking;

public static class ForwardedHeadersExtensions
{
    /// <summary>Binds and validates <see cref="TrustedProxyOptions"/> and turns them into the framework's
    /// <see cref="ForwardedHeadersOptions"/>: only the configured proxies and networks are trusted (the framework's
    /// loopback defaults are dropped).</summary>
    public static IServiceCollection AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TrustedProxyOptions>()
            .Bind(configuration.GetSection(TrustedProxyOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TrustedProxyOptions>, TrustedProxyOptionsValidator>();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<TrustedProxyOptions>>((forwarded, trusted) =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwarded.ForwardLimit = trusted.Value.ForwardLimit;
                forwarded.KnownProxies.Clear();
                forwarded.KnownIPNetworks.Clear();
                foreach (var proxy in trusted.Value.KnownProxies)
                    forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
                foreach (var network in trusted.Value.KnownNetworks)
                    forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            });

        return services;
    }

    /// <summary>
    /// Reads <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c> from trusted proxies. Must run before anything that reads
    /// the client IP or the scheme. With no trusted proxy configured it is not added at all: the framework middleware
    /// with empty known lists would trust every sender, which is the opposite of what "none configured" means.
    /// </summary>
    public static IApplicationBuilder UseTrustedForwardedHeaders(this IApplicationBuilder app)
    {
        var trusted = app.ApplicationServices.GetRequiredService<IOptions<TrustedProxyOptions>>().Value;
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ForwardedHeadersExtensions));
        var environment = app.ApplicationServices.GetRequiredService<IHostEnvironment>();

        if (!trusted.TrustsAnyProxy)
        {
            if (environment.IsDevelopment())
                logger.LogInformation("No trusted proxy is configured; X-Forwarded-* headers are ignored.");
            else
                logger.LogWarning(
                    "No trusted proxy is configured (ForwardedHeaders:KnownNetworks); X-Forwarded-* headers are ignored. Behind an ingress every client then shares its address in the rate limits.");
            return app;
        }

        return app.UseForwardedHeaders();
    }
}
