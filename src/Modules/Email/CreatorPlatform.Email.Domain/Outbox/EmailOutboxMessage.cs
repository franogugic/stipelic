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
        string? replyTo,
        string? listUnsubscribeUrl,
        DateTimeOffset createdAt)
    {
        Id = id;
        Purpose = purpose;
        CorrelationKey = correlationKey;
        ToEmail = toEmail;
        Subject = subject;
        HtmlBody = htmlBody;
        PlainTextBody = plainTextBody;
        ReplyTo = replyTo;
        ListUnsubscribeUrl = listUnsubscribeUrl;
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
        DateTimeOffset createdAt,
        string? replyTo = null,
        string? listUnsubscribeUrl = null)
    {
        return new EmailOutboxMessage(
            Guid.NewGuid(),
            purpose,
            correlationKey,
            toEmail,
            subject,
            htmlBody,
            plainTextBody,
            replyTo,
            listUnsubscribeUrl,
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

    /// <summary>Provider-side throttling (e.g. ACS 429) is not a delivery failure — it means "try later",
    /// not "this attempt failed". Pushes the processing lease out to <paramref name="nextAttemptAt"/> so
    /// the claim query's existing "Processing AND ProcessingExpiresAt &lt;= now" reclaim path picks this
    /// message back up, WITHOUT touching <see cref="RetryCount"/> or <see cref="Status"/> (still
    /// Processing) — a throttled send must never count against the message's limited retry budget.</summary>
    public void Reschedule(DateTimeOffset nextAttemptAt)
    {
        ProcessingExpiresAt = nextAttemptAt;
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

    /// <summary>Reply-To address for this message (e.g. a creator's SupportEmail) — null for transactional
    /// mail that doesn't need it (verification, order access).</summary>
    public string? ReplyTo { get; private set; }

    /// <summary>Per-recipient unsubscribe URL. When set, the sender attaches List-Unsubscribe +
    /// List-Unsubscribe-Post headers (RFC 8058 one-click) — null for transactional mail.</summary>
    public string? ListUnsubscribeUrl { get; private set; }

    public EmailOutboxMessageStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? ProcessingExpiresAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
