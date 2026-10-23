using CreatorPlatform.Api.Responses;

namespace CreatorPlatform.Api.Security;

/// <summary>
/// CSRF guard: every request that can change data (anything but GET, HEAD, OPTIONS, TRACE) must carry
/// <c>X-Requested-With: luma</c>, which the SPA always sends. A page on another site cannot add a custom header without a
/// CORS preflight, and the preflight only succeeds for the configured origins — so a cross-site form or no-cors fetch
/// riding the session cookie is refused here, even for endpoints that take no body. Exempt: Stripe's webhooks (signed)
/// and the public endpoints (no session; the RFC 8058 one-click unsubscribe POST comes from mail clients).
/// </summary>
public sealed class AntiForgeryHeaderMiddleware
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "luma";
    public const string ErrorCode = "ANTI_FORGERY_HEADER_MISSING";

    private static readonly PathString[] ExemptPrefixes = [new("/webhooks/stripe"), new("/api/public")];

    private readonly RequestDelegate _next;
    private readonly ILogger<AntiForgeryHeaderMiddleware> _logger;

    public AntiForgeryHeaderMiddleware(RequestDelegate next, ILogger<AntiForgeryHeaderMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSafeMethod(context.Request.Method) || IsExempt(context.Request.Path)
            || string.Equals(context.Request.Headers[HeaderName], HeaderValue, StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "Refused a {Method} {Path} without the anti-forgery header. Origin: {Origin}",
            context.Request.Method, context.Request.Path, context.Request.Headers.Origin.ToString());

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status403Forbidden,
            Message = "This request is missing the anti-forgery header.",
            Code = ErrorCode
        });
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    // StartsWithSegments: "/api/public/…" is exempt, "/api/publicity" is not.
    private static bool IsExempt(PathString path) =>
        ExemptPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
