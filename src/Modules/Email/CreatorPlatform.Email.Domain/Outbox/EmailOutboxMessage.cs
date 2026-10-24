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
        DateTimeOffset createdAt,
        int? creatorId,
        long? queuePosition)
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
        CreatorId = creatorId;
        QueuePosition = queuePosition;
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
        string? listUnsubscribeUrl = null,
        int? creatorId = null,
        long? queuePosition = null)
    {
        if (purpose == EmailOutboxMessagePurpose.CampaignBroadcast && (creatorId is null) != (queuePosition is null))
            throw new ArgumentException("A campaign message gets its creator and its queue position together.");

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
            createdAt,
            creatorId,
            queuePosition);
    }

    /// <summary>Transactional mail (everything but campaigns) is sent before any campaign mail.</summary>
    public bool IsTransactional => Purpose != EmailOutboxMessagePurpose.CampaignBroadcast;

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

    /// <summary>Creator-initiated manual retry of a terminally-Failed message (e.g. "Resend failed" on a
    /// campaign) — resets the retry budget and puts it back in the normal pending queue. Guarded to only
    /// leave Failed, since requeuing a Sent/Cancelled/Processing message would be nonsensical (or, for
    /// Processing, could race the worker holding that lease).</summary>
    public void Requeue(DateTimeOffset nextAttemptAt)
    {
        if (Status != EmailOutboxMessageStatus.Failed)
            return;

        Status = EmailOutboxMessageStatus.Pending;
        RetryCount = 0;
        NextAttemptAt = nextAttemptAt;
        LastError = null;
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

    /// <summary>The <c>LastError</c> column's length (varchar(1000)); a longer error is cut, never rejected by the
    /// database (which would leave the message stuck in Processing).</summary>
    public const int LastErrorMaxLength = 1000;

    public void MarkAsFailed(string error, DateTimeOffset nextAttemptAt, int maxRetryCount)
    {
        RetryCount++;
        LastError = error.Length > LastErrorMaxLength
            ? error[..LastErrorMaxLength]
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

    /// <summary>Null once the message has been sent and is past its retention: the maintenance job drops the bodies of
    /// old sent mail (a campaign stores one full body per recipient) and keeps the row. Never null on a message that can
    /// still be sent: only <see cref="EmailOutboxMessageStatus.Sent"/> rows are purged, and those are never sent again.</summary>
    public string? HtmlBody { get; private set; }

    /// <inheritdoc cref="HtmlBody"/>
    public string? PlainTextBody { get; private set; }

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

    /// <summary>The creator the message is sent for: a campaign's (the unit of fairness), and a buyer's or lead's email
    /// too, so deleting the workspace deletes its mail. Null for account mail (verification, password reset, …).</summary>
    public int? CreatorId { get; private set; }

    /// <summary>
    /// Campaign messages only: the order in which the outbox sends them (start-time fair queuing). A creator's campaign is
    /// numbered from the head of the campaign queue, or from after that creator's own mail still waiting, whichever is
    /// later. So every creator with mail waiting gets an equal share of sending, however big its list, and a creator's
    /// second campaign waits behind its first instead of doubling its share. Assigned by the outbox service when queuing.
    /// </summary>
    public long? QueuePosition { get; private set; }
}
