using System.Net;
using System.Net.Sockets;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>A local port nothing listens on, for "the server is down" tests: connecting is refused at once.</summary>
public static class ClosedPort
{
    public static int Find()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
