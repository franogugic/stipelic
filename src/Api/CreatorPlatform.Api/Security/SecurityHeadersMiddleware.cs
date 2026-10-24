namespace CreatorPlatform.Api.Security;

/// <summary>
/// Headers on every API response, errors and rate-limit rejections included: no MIME sniffing; no Referer at all
/// (the tracked-link redirect then never hands a token URL to the destination); no powerful browser features; and a CSP
/// that lets nothing load or frame an API response — the API serves JSON, redirects and a tracking GIF, never a page.
/// HSTS is the framework's middleware, outside Development.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    public const string PermissionsPolicy =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
        return _next(context);
    }
}
