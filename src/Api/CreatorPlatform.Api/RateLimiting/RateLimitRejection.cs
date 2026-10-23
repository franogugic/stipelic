using System.Globalization;
using System.Threading.RateLimiting;
using CreatorPlatform.Api.Responses;

namespace CreatorPlatform.Api.RateLimiting;

public static class RateLimitRejection
{
    public const string Code = "RATE_LIMITED";

    /// <summary>The 429 body every rate limit answers, and its <c>Retry-After</c> header (whole seconds) when the lease
    /// knows it — shared by the middleware's rejections and the limits an action applies itself.</summary>
    public static ApiErrorResponse Body(HttpResponse response, RateLimitLease lease)
    {
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            Message = "Too many requests. Please try again later.",
            Code = Code
        };
    }
}
