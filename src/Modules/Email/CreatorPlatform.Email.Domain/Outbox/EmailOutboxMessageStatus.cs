namespace CreatorPlatform.Email.Domain.Outbox;

public enum EmailOutboxMessageStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}
