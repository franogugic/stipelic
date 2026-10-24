namespace CreatorPlatform.Api.Security;

public static class SecurityExtensions
{
    /// <summary>HSTS (180 days, this host only — the web app's host decides for itself) and the start-up guard on
    /// <c>AllowedHosts</c>: outside Development it must list the API's real host names, never "*", so a request carrying
    /// any other Host header is refused (400) by the framework's host filtering.</summary>
    public static IServiceCollection AddPlatformSecurity(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            var allowedHosts = configuration["AllowedHosts"]?
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            if (allowedHosts.Length == 0 || allowedHosts.Contains("*"))
            {
                throw new InvalidOperationException(
                    "AllowedHosts must list the API's host names (e.g. \"api.luma.hr\") outside Development, not \"*\".");
            }
        }

        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(180);
            options.IncludeSubDomains = false;
            options.Preload = false;
        });

        return services;
    }

    /// <summary>After the forwarded headers (HSTS needs the real scheme), before anything that can answer.</summary>
    public static IApplicationBuilder UsePlatformSecurityHeaders(this IApplicationBuilder app, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            app.UseHsts();

        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
