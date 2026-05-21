namespace CreatorPlatform.Email.Domain.Outbox;

public sealed class EmailOutboxMessage
{
    private EmailOutboxMessage()
    {
    }

    private EmailOutboxMessage(
        Guid id,
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        DateTimeOffset createdAt)
    {
        Id = id;
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
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        DateTimeOffset createdAt)
    {
        return new EmailOutboxMessage(
            Guid.NewGuid(),
            toEmail,
            subject,
            htmlBody,
            plainTextBody,
            createdAt);
    }

    public Guid Id { get; private set; }

    public string ToEmail { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string HtmlBody { get; private set; } = string.Empty;

    public string PlainTextBody { get; private set; } = string.Empty;

    public EmailOutboxMessageStatus Status { get; private set; }

    public int RetryCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
