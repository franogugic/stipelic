using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>One throwaway Mailpit (the same image as docker-compose.yml) per collection, with its HTTP API for reading what
/// arrived. Tests isolate themselves by sending to their own unique recipient.</summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _container = new ContainerBuilder("axllent/mailpit:v1.31.6")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(HttpPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/readyz")))
        .Build();

    private HttpClient? _http;

    public string Host => _container.Hostname;

    public int SmtpHostPort => _container.GetMappedPublicPort(SmtpPort);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpPort)}") };
    }

    public async Task DisposeAsync()
    {
        _http?.Dispose();
        await _container.DisposeAsync();
    }

    private HttpClient Http => _http ?? throw new InvalidOperationException("The Mailpit fixture is not initialized.");

    /// <summary>The one message sent to <paramref name="toEmail"/>: its summary JSON (From, To, ReplyTo, Subject, Text,
    /// HTML), its headers and its raw source.</summary>
    public async Task<(JsonElement Message, Dictionary<string, string[]> Headers, string Raw)> GetSingleMessageToAsync(string toEmail)
    {
        var search = await Http.GetFromJsonAsync<JsonElement>($"/api/v1/search?query={Uri.EscapeDataString($"to:\"{toEmail}\"")}");
        var id = search.GetProperty("messages").EnumerateArray().Single().GetProperty("ID").GetString();

        var message = await Http.GetFromJsonAsync<JsonElement>($"/api/v1/message/{id}");
        var headers = (await Http.GetFromJsonAsync<Dictionary<string, string[]>>($"/api/v1/message/{id}/headers"))!;
        var raw = await Http.GetStringAsync($"/api/v1/message/{id}/raw");
        return (message, headers, raw);
    }
}
