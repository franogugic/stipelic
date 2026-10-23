using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Email;

/// <summary>A provider error longer than the column used to make the save fail, leaving the message stuck in Processing.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EmailOutboxLastErrorTests
{
    private readonly PostgresFixture _postgres;

    public EmailOutboxLastErrorTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task AVeryLongProviderError_IsStored_AndTheMessageMovesOn()
    {
        var now = DateTimeOffset.UtcNow;
        var message = EmailOutboxMessage.Create(
            EmailOutboxMessagePurpose.OrderAccess, Guid.NewGuid().ToString(), TestData.UniqueEmail("long-error"), "s", "<p>b</p>", "b", now);
        await using (var db = _postgres.CreateDbContext())
        {
            db.Add(message);
            await db.SaveChangesAsync();
        }

        await using (var db = _postgres.CreateDbContext())
        {
            db.Attach(message);
            message.MarkAsProcessing(now.AddMinutes(5));
            message.MarkAsFailed(new string('e', 1500), now.AddMinutes(1), maxRetryCount: 3);
            await db.SaveChangesAsync();
        }

        await using (var db = _postgres.CreateDbContext())
        {
            var stored = await db.Set<EmailOutboxMessage>().AsNoTracking().SingleAsync(row => row.Id == message.Id);
            Assert.Equal((EmailOutboxMessageStatus.Pending, 1, 1000), (stored.Status, stored.RetryCount, stored.LastError!.Length));
        }
    }
}
