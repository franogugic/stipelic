namespace CreatorPlatform.Email.Domain.Outbox;

public sealed class EmailOutboxMessage
{
    private EmailOutboxMessage()
    {
    }

    private EmailOutboxMessage(
        Guid id,
        EmailOutboxMessagePurpose purpose,
        string correlationKey,
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        DateTimeOffset createdAt)
    {
        Id = id;
        Purpose = purpose;
        CorrelationKey = correlationKey;
        ToEmail = toEmail;
        Subject = subject;
        HtmlBody = htmlBody;
        PlainTextBody = plainTextBody;
        Status = EmailOutboxMessageStatus.Pending;
        RetryCount = 0;
        NextAttemptAt = createdAt;
        CreatedAt = createdAt;
    }

    public static EmailOutboxMessage Create(
        EmailOutboxMessagePurpose purpose,
        string correlationKey,
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        DateTimeOffset createdAt)
    {
        return new EmailOutboxMessage(
            Guid.NewGuid(),
            purpose,
            correlationKey,
            toEmail,
            subject,
            htmlBody,
            plainTextBody,
            createdAt);
    }

    public void MarkAsSent(DateTimeOffset sentAt)
    {
        Status = EmailOutboxMessageStatus.Sent;
        SentAt = sentAt;
        LastError = null;
        ProcessingExpiresAt = null;
    }

    public void MarkAsProcessing(DateTimeOffset processingExpiresAt)
    {
        Status = EmailOutboxMessageStatus.Processing;
        ProcessingExpiresAt = processingExpiresAt;
    }

    public void Cancel()
    {
        if (Status is not EmailOutboxMessageStatus.Pending and
            not EmailOutboxMessageStatus.Processing)
            return;

        Status = EmailOutboxMessageStatus.Cancelled;
        ProcessingExpiresAt = null;
        LastError = null;
    }

    public void MarkAsFailed(string error, DateTimeOffset nextAttemptAt, int maxRetryCount)
    {
        RetryCount++;
        LastError = error.Length > 2000
            ? error[..2000]
            : error;

        if (RetryCount >= maxRetryCount)
        {
            Status = EmailOutboxMessageStatus.Failed;
            ProcessingExpiresAt = null;
            return;
        }

        Status = EmailOutboxMessageStatus.Pending;
        NextAttemptAt = nextAttemptAt;
        ProcessingExpiresAt = null;
    }

    public Guid Id { get; private set; }

    public EmailOutboxMessagePurpose Purpose { get; private set; }

    public string CorrelationKey { get; private set; } = string.Empty;

    public string ToEmail { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string HtmlBody { get; private set; } = string.Empty;

    public string PlainTextBody { get; private set; } = string.Empty;

    public EmailOutboxMessageStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? ProcessingExpiresAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
