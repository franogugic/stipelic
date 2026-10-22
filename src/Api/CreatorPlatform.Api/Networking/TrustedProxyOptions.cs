namespace CreatorPlatform.Api.Networking;

/// <summary>
/// The <c>ForwardedHeaders</c> section: which proxies may tell the API the client's address and scheme
/// (<c>X-Forwarded-For</c>, <c>X-Forwarded-Proto</c>). Empty by default: then no forwarded header is read at all and
/// the client IP is the connection's address. Behind Azure Container Apps, <see cref="KnownNetworks"/> must hold the
/// environment's infrastructure subnet, or every user shares the ingress's address in every rate limit.
/// </summary>
public sealed class TrustedProxyOptions
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Single proxy addresses, e.g. <c>10.0.0.5</c>.</summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>Proxy networks in CIDR notation, e.g. <c>10.0.0.0/23</c>.</summary>
    public List<string> KnownNetworks { get; set; } = [];

    /// <summary>How many proxies' entries are read from the end of <c>X-Forwarded-For</c>; 1 = only what our own
    /// ingress appended, so a client cannot choose its address by sending the header itself.</summary>
    public int ForwardLimit { get; set; } = 1;

    public bool TrustsAnyProxy => KnownProxies.Count > 0 || KnownNetworks.Count > 0;
}
