using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Api.Security;

public static class FrontendCorsExtensions
{
    /// <summary>Binds and validates <see cref="FrontendCorsOptions"/> and registers the <c>Frontend</c> CORS policy over
    /// the configured origins, with credentials. Any request header is allowed — the SPA's anti-forgery
    /// <c>X-Requested-With</c> among them.</summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FrontendCorsOptions>()
            .Bind(configuration.GetSection(FrontendCorsOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<FrontendCorsOptions>, FrontendCorsOptionsValidator>();

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<FrontendCorsOptions>, IHostEnvironment>((cors, frontend, environment) =>
                cors.AddPolicy(FrontendCorsOptions.PolicyName, policy => policy
                    .WithOrigins([.. frontend.Value.EffectiveOrigins(environment)])
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()));

        return services;
    }
}
