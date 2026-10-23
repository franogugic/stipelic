using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Core;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Outbox;
using CreatorPlatform.Email.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Email.Outbox;

/// <summary>Records what the outbox sends (in send order), how many sends overlapped, and fails on demand.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private int _inFlight;
    private int _maxInFlight;

    public ConcurrentQueue<string> Sent { get; } = new();

    public TimeSpan Delay { get; set; }

    /// <summary>Returns the exception to throw for a recipient, or null to send.</summary>
    public Func<string, Exception?> FailWith { get; set; } = _ => null;

    public int MaxInFlight => Volatile.Read(ref _maxInFlight);

    public async Task SendAsync(
        string toEmail, string subject, string htmlBody, string plainTextBody, string? replyTo, string? listUnsubscribeUrl,
        CancellationToken ct)
    {
        var inFlight = Interlocked.Increment(ref _inFlight);
        int max;
        while (inFlight > (max = Volatile.Read(ref _maxInFlight)) && Interlocked.CompareExchange(ref _maxInFlight, inFlight, max) != max)
        {
        }

        try
        {
            if (Delay > TimeSpan.Zero)
                await Task.Delay(Delay, ct);
            if (FailWith(toEmail) is { } exception)
                throw exception;
            Sent.Enqueue(toEmail);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }
}

/// <summary>An HTTP response carrying only a status and headers, to build the provider's 429.</summary>
public sealed class HeaderOnlyResponse(int status, IReadOnlyDictionary<string, string> headers) : Response
{
    public override int Status => status;

    public override string ReasonPhrase => "Too Many Requests";

    public override Stream? ContentStream { get; set; }

    public override string ClientRequestId { get; set; } = Guid.NewGuid().ToString();

    public override void Dispose()
    {
    }

    protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value) =>
        headers.TryGetValue(name, out value);

    protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        values = headers.TryGetValue(name, out var value) ? [value] : null;
        return values is not null;
    }

    protected override bool ContainsHeader(string name) => headers.ContainsKey(name);

    protected override IEnumerable<HttpHeader> EnumerateHeaders() => headers.Select(header => new HttpHeader(header.Key, header.Value));

    public static RequestFailedException Throttled(int retryAfterSeconds) =>
        new(new HeaderOnlyResponse(429, new Dictionary<string, string> { ["Retry-After"] = retryAfterSeconds.ToString() }));
}

/// <summary>A dispatcher wired like the Worker's, on an isolated database, with a fake sender.</summary>
public sealed class OutboxHarness : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    public OutboxHarness(
        IsolatedDatabase database,
        EmailOutboxOptions options,
        TimeProvider timeProvider,
        RecordingEmailSender? sender = null,
        ILogger<EmailOutboxDispatcher>? logger = null,
        params IEmailSendFailureHandler[] failureHandlers)
    {
        Database = database;
        Sender = sender ?? new RecordingEmailSender();

        var services = new ServiceCollection();
        services.AddDbContext<CreatorPlatformDbContext>(builder => builder
            .UseNpgsql(database.ConnectionString)
            .UseEntityConfigurationsFrom(CreatorPlatform.Api.Hosting.ApiEntityConfigurations.Assemblies));
        services.AddSingleton<IEmailSender>(Sender);
        foreach (var handler in failureHandlers)
            services.AddSingleton(handler);
        _services = services.BuildServiceProvider();

        Throttle = new EmailSendThrottle(Microsoft.Extensions.Options.Options.Create(options), timeProvider);
        Dispatcher = new EmailOutboxDispatcher(
            _services.GetRequiredService<IServiceScopeFactory>(),
            Throttle,
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { ThrottleBackoffMinutes = 15 }),
            timeProvider,
            logger ?? NullLogger<EmailOutboxDispatcher>.Instance);
    }

    public IsolatedDatabase Database { get; }

    public RecordingEmailSender Sender { get; }

    public EmailSendThrottle Throttle { get; }

    public EmailOutboxDispatcher Dispatcher { get; }

    public static EmailOutboxOptions Options(
        int batchSize = 10, int parallel = 1, int perMinute = 10_000, int perHour = 100_000, int reservePercent = 0) => new()
    {
        BatchSize = batchSize,
        MaxParallelSends = parallel,
        MaxSendsPerMinute = perMinute,
        MaxSendsPerHour = perHour,
        TransactionalReservePercent = reservePercent,
        PollIntervalSeconds = 5,
        ProcessingLeaseMinutes = 5,
        SendTimeoutSeconds = 30,
    };

    /// <summary>Passes until a pass claims nothing.</summary>
    public async Task<int> DrainAsync(int maxPasses = 1000)
    {
        var total = 0;
        for (var pass = 0; pass < maxPasses; pass++)
        {
            var result = await Dispatcher.DispatchOnceAsync(CancellationToken.None);
            if (result.Claimed == 0)
                return total;
            total += result.Claimed;
        }

        throw new InvalidOperationException("The outbox did not drain.");
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}

public static class OutboxQueue
{
    /// <summary>Queues one campaign's mail the way the send pipeline does (one scope, positions assigned on the way).</summary>
    public static async Task<List<string>> CampaignAsync(IsolatedDatabase database, int creatorId, string label, int count)
    {
        await using var db = database.CreateDbContext();
        var service = new EmailOutboxService(db, Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.test" }));
        var campaign = Guid.NewGuid();
        var recipients = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var to = $"{label}-{i:D3}@example.test";
            await service.QueueCampaignAsync(creatorId, to, "s", "<p>b</p>", "b", null, "https://api.test/u", $"{campaign}:{i}", CancellationToken.None);
            recipients.Add(to);
        }

        await db.SaveChangesAsync();
        return recipients;
    }

    public static async Task<string> TransactionalAsync(IsolatedDatabase database, string label, EmailOutboxMessagePurpose purpose = EmailOutboxMessagePurpose.OrderAccess)
    {
        await using var db = database.CreateDbContext();
        var to = $"{label}@example.test";
        db.Add(EmailOutboxMessage.Create(purpose, Guid.NewGuid().ToString(), to, "s", "<p>b</p>", "b", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return to;
    }

    public static async Task<EmailOutboxMessage> GetAsync(IsolatedDatabase database, string toEmail)
    {
        await using var db = database.CreateDbContext();
        return await db.Set<EmailOutboxMessage>().AsNoTracking().SingleAsync(message => message.ToEmail == toEmail);
    }
}

/// <summary>A manual clock set to the real time (the queued rows use the real time), moved only by the test.</summary>
public static class OutboxClock
{
    public static ManualTimeProvider Now()
    {
        var clock = new ManualTimeProvider();
        clock.Advance(DateTimeOffset.UtcNow.AddSeconds(1) - clock.GetUtcNow());
        return clock;
    }
}
