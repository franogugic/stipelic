using CreatorPlatform.Email.Domain.Outbox;

namespace CreatorPlatform.MoneyPath.Tests.Email;

public class EmailOutboxMessageTests
{
    private static EmailOutboxMessage BuildProcessingMessage(DateTimeOffset now)
    {
        var message = EmailOutboxMessage.Create(
            EmailOutboxMessagePurpose.CampaignBroadcast,
            $"{Guid.NewGuid()}:1",
            "a@test.com",
            "Subject",
            "<p>Body</p>",
            "Body",
            now);

        message.MarkAsProcessing(now.AddMinutes(5));
        return message;
    }

    [Fact]
    public void Reschedule_DoesNotChangeRetryCountOrStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var message = BuildProcessingMessage(now);
        var nextAttemptAt = now.AddMinutes(15);

        message.Reschedule(nextAttemptAt);

        Assert.Equal(EmailOutboxMessageStatus.Processing, message.Status);
        Assert.Equal(0, message.RetryCount);
        Assert.Equal(nextAttemptAt, message.ProcessingExpiresAt);
    }

    [Fact]
    public void Reschedule_DoesNotClearLastError()
    {
        var now = DateTimeOffset.UtcNow;
        var message = BuildProcessingMessage(now);

        // Simulate a prior generic-failure retry before a throttled attempt, to prove Reschedule doesn't
        // wipe state MarkAsFailed set on an earlier attempt.
        message.MarkAsFailed("transient error", now.AddMinutes(1), maxRetryCount: 3);
        message.MarkAsProcessing(now.AddMinutes(2));

        message.Reschedule(now.AddMinutes(15));

        Assert.Equal(1, message.RetryCount);
        Assert.NotNull(message.LastError);
    }

    [Fact]
    public void MarkAsFailed_BelowMaxRetryCount_StaysPendingAndIncrementsRetryCount()
    {
        var now = DateTimeOffset.UtcNow;
        var message = BuildProcessingMessage(now);

        message.MarkAsFailed("boom", now.AddMinutes(1), maxRetryCount: 3);

        Assert.Equal(EmailOutboxMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.RetryCount);
    }

    [Fact]
    public void MarkAsFailed_AtMaxRetryCount_BecomesTerminallyFailed()
    {
        var now = DateTimeOffset.UtcNow;
        var message = BuildProcessingMessage(now);

        message.MarkAsFailed("boom 1", now.AddMinutes(1), maxRetryCount: 2);
        message.MarkAsProcessing(now.AddMinutes(2));
        message.MarkAsFailed("boom 2", now.AddMinutes(3), maxRetryCount: 2);

        Assert.Equal(EmailOutboxMessageStatus.Failed, message.Status);
        Assert.Equal(2, message.RetryCount);
    }
}
