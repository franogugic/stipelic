using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Outbox;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Integration.Tests.Email.Outbox;

/// <summary>B6: a transactional email that failed for good is an Error with its own event (for alerting) and names what it
/// belonged to, never the address; a campaign email that failed for good is only a Warning.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TransactionalEmailFailureLogTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedDatabase _database = null!;

    public TransactionalEmailFailureLogTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync() => _database = await IsolatedDatabase.CreateAsync(_postgres);

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private sealed class EventLogger : ILogger<EmailOutboxDispatcher>
    {
        public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
                Entries.Add((logLevel, eventId, formatter(state, exception)));
        }
    }

    private async Task<EventLogger> FailForGoodAsync()
    {
        var clock = OutboxClock.Now();
        var logger = new EventLogger();
        await using var outbox = new OutboxHarness(_database, OutboxHarness.Options(batchSize: 10), clock, logger: logger);
        outbox.Sender.FailWith = _ => new InvalidOperationException("550 mailbox unavailable");

        for (var attempt = 0; attempt < EmailOutboxDispatcher.MaxRetryCount; attempt++)
        {
            await outbox.Dispatcher.DispatchOnceAsync(CancellationToken.None);
            clock.Advance(TimeSpan.FromMinutes(16));
        }

        return logger;
    }

    [Fact]
    public async Task AnOrderEmailThatFailedForGood_IsAnErrorToAlertOn_NamingTheOrderNotTheBuyer()
    {
        var buyer = await OutboxQueue.TransactionalAsync(_database, "buyer", EmailOutboxMessagePurpose.OrderAccess);
        var orderKey = (await OutboxQueue.GetAsync(_database, buyer)).CorrelationKey;

        var logger = await FailForGoodAsync();

        var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal(EmailOutboxDispatcher.TransactionalEmailFailed, error.EventId);
        Assert.Contains("OrderAccess", error.Message);
        Assert.Contains(orderKey, error.Message);
        Assert.DoesNotContain(buyer, error.Message);
        Assert.Equal(EmailOutboxMessageStatus.Failed, (await OutboxQueue.GetAsync(_database, buyer)).Status);
    }

    [Fact]
    public async Task ACampaignEmailThatFailedForGood_IsOnlyAWarning()
    {
        await OutboxQueue.CampaignAsync(_database, creatorId: 1, "list", 1);

        var logger = await FailForGoodAsync();

        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
        Assert.Single(logger.Entries, entry => entry.EventId == EmailOutboxDispatcher.CampaignEmailFailed && entry.Level == LogLevel.Warning);
    }
}
