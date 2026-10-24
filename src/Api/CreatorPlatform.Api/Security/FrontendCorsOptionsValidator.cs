using System.Net;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Api.Security;

/// <summary>Fails the start on an origin that is not a bare origin, and — outside Development — on no origin at all, a
/// plain-http one or a localhost one: credentials must only ever be shared with the real web app.</summary>
public sealed class FrontendCorsOptionsValidator : IValidateOptions<FrontendCorsOptions>
{
    private readonly IHostEnvironment _environment;

    public FrontendCorsOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, FrontendCorsOptions options)
    {
        var failures = new List<string>();
        var development = _environment.IsDevelopment();

        if (!development && options.AllowedOrigins.Count == 0)
            failures.Add("Cors:AllowedOrigins must list the web app's origin outside Development.");

        foreach (var origin in options.AllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || uri.AbsolutePath != "/" || origin.EndsWith('/') || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                failures.Add($"Cors:AllowedOrigins has an entry that is not an origin (scheme://host[:port]): '{origin}'.");
                continue;
            }

            if (development)
                continue;

            if (uri.Scheme != "https")
                failures.Add($"Cors:AllowedOrigins must be https outside Development: '{origin}'.");
            if (uri.IsLoopback || IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address))
                failures.Add($"Cors:AllowedOrigins must not be localhost outside Development: '{origin}'.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
