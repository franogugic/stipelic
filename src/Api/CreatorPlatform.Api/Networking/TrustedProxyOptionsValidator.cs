using System.Net;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Api.Networking;

/// <summary>Fails the start on an address or network that does not parse, so a typo can never silently trust
/// nothing (or the wrong range).</summary>
public sealed class TrustedProxyOptionsValidator : IValidateOptions<TrustedProxyOptions>
{
    public const int MaxForwardLimit = 10;

    public ValidateOptionsResult Validate(string? name, TrustedProxyOptions options)
    {
        var failures = new List<string>();

        foreach (var proxy in options.KnownProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
                failures.Add($"ForwardedHeaders:KnownProxies has an entry that is not an IP address: '{proxy}'.");
        }

        foreach (var network in options.KnownNetworks)
        {
            if (!IPNetwork.TryParse(network, out _))
                failures.Add($"ForwardedHeaders:KnownNetworks has an entry that is not a CIDR network: '{network}'.");
        }

        if (options.ForwardLimit is < 1 or > MaxForwardLimit)
            failures.Add($"ForwardedHeaders:ForwardLimit must be between 1 and {MaxForwardLimit}.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
