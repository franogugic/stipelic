namespace CreatorPlatform.Api.Security;

/// <summary>
/// The <c>Cors</c> section: the web app's origins, the only ones allowed to call the API with credentials (the session
/// cookie). Outside Development at least one is required, each https and none on localhost; in Development an empty list
/// falls back to <see cref="DevelopmentOrigins"/>.
/// </summary>
public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Frontend";

    /// <summary>The Vite dev servers.</summary>
    public static readonly IReadOnlyList<string> DevelopmentOrigins =
        ["http://localhost:3000", "http://localhost:3001", "http://127.0.0.1:3000", "http://127.0.0.1:3001"];

    /// <summary>Scheme, host and port only, e.g. <c>https://app.luma.hr</c>.</summary>
    public List<string> AllowedOrigins { get; set; } = [];

    public IReadOnlyList<string> EffectiveOrigins(IHostEnvironment environment) =>
        AllowedOrigins.Count == 0 && environment.IsDevelopment() ? DevelopmentOrigins : AllowedOrigins;
}
