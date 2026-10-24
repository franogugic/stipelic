using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Maintenance;
using CreatorPlatform.Email.Infrastructure.Persistence.Configurations;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Maintenance;

/// <summary>U2: bodies of mail sent more than 30 days ago are dropped, the row stays; nothing else loses its body.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PurgeSentEmailBodiesTaskTests
{
    private readonly PostgresFixture _postgres;

    public PurgeSentEmailBodiesTaskTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<Guid> AddAsync(string toEmail, EmailOutboxMessageStatus status, DateTimeOffset createdAt)
    {
        var message = EmailOutboxMessage.Create(
            EmailOutboxMessagePurpose.CampaignBroadcast, $"{Guid.NewGuid()}:1", toEmail, "Subject", "<p>Body</p>", "Body", createdAt);

        switch (status)
        {
            case EmailOutboxMessageStatus.Sent:
                message.MarkAsProcessing(createdAt.AddMinutes(5));
                message.MarkAsSent(createdAt);
                break;
            case EmailOutboxMessageStatus.Failed:
                message.MarkAsFailed("boom", createdAt, maxRetryCount: 1);
                break;
            case EmailOutboxMessageStatus.Cancelled:
                message.Cancel();
                break;
        }

        await using var db = _postgres.CreateDbContext();
        db.Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private async Task<EmailOutboxMessage> GetAsync(Guid id)
    {
        await using var db = _postgres.CreateDbContext();
        return await db.Set<EmailOutboxMessage>().AsNoTracking().SingleAsync(message => message.Id == id);
    }

    private PurgeSentEmailBodiesTask Task(int batchSize = 1000)
    {
        var db = _postgres.CreateDbContext();
        return new PurgeSentEmailBodiesTask(db, Options.Create(new MaintenanceOptions { BatchSize = batchSize }));
    }

    [Fact]
    public async Task OldSentMail_LosesItsBodies_InBatches_AndKeepsTheRow()
    {
        var to = TestData.UniqueEmail("purge");
        var old = new List<Guid>();
        for (var i = 0; i < 5; i++)
            old.Add(await AddAsync(to, EmailOutboxMessageStatus.Sent, Now.AddDays(-31).AddMinutes(i)));

        var purged = await Task(batchSize: 2).RunAsync(Now, CancellationToken.None);

        Assert.True(purged >= 5);
        foreach (var id in old)
        {
            var message = await GetAsync(id);
            Assert.Null(message.HtmlBody);
            Assert.Null(message.PlainTextBody);
            Assert.Equal((EmailOutboxMessageStatus.Sent, "Subject", to), (message.Status, message.Subject, message.ToEmail));
            Assert.NotNull(message.SentAt);
        }

        Assert.Equal(0, await Task().RunAsync(Now, CancellationToken.None));   // idempotent
    }

    [Fact]
    public async Task RecentSentMail_AndMailThatWasNeverSent_KeepTheirBodies()
    {
        var to = TestData.UniqueEmail("keep");
        var kept = new[]
        {
            await AddAsync(to, EmailOutboxMessageStatus.Sent, Now.AddDays(-29)),
            await AddAsync(to, EmailOutboxMessageStatus.Failed, Now.AddDays(-90)),      // can still be resent
            await AddAsync(to, EmailOutboxMessageStatus.Cancelled, Now.AddDays(-90)),
            await AddAsync(to, EmailOutboxMessageStatus.Pending, Now.AddDays(-90)),
        };

        await Task().RunAsync(Now, CancellationToken.None);

        foreach (var id in kept)
        {
            var message = await GetAsync(id);
            Assert.Equal(("<p>Body</p>", "Body"), (message.HtmlBody, message.PlainTextBody));
        }
    }

    [Fact]
    public async Task TheWorkList_RepeatsThePartialIndexFilter_SoPostgresCanUseIt()
    {
        await using var db = _postgres.CreateDbContext();

        var sql = PurgeSentEmailBodiesTask.Candidates(db, Now).Select(message => message.SentAt).ToQueryString();

        Assert.Contains("\"Status\" = 'Sent'", sql);
        Assert.Contains("\"HtmlBody\" IS NOT NULL", sql);
        Assert.Equal("\"Status\" = 'Sent' AND \"HtmlBody\" IS NOT NULL", EmailOutboxMessageConfiguration.SentWithBodiesIndexFilter);
    }
}
