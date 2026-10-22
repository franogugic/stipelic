using System.Net;

namespace CreatorPlatform.Api.Networking;

/// <summary>The one place that decides a request's client IP (rate-limit partitions, the login limiter). Behind a
/// trusted proxy the forwarded-headers middleware has already replaced the connection's address with the client's.</summary>
public static class ClientIp
{
    public const string Unknown = "unknown";

    public static string From(HttpContext httpContext) => From(httpContext.Connection.RemoteIpAddress);

    /// <summary>An IPv4 client reaching a dual-stack socket shows up as <c>::ffff:a.b.c.d</c>; it is the same client
    /// as <c>a.b.c.d</c> and must share its partitions.</summary>
    public static string From(IPAddress? address) => address switch
    {
        null => Unknown,
        { IsIPv4MappedToIPv6: true } => address.MapToIPv4().ToString(),
        _ => address.ToString()
    };
}
