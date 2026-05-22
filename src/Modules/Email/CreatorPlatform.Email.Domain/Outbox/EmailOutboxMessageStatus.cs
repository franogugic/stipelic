namespace CreatorPlatform.Email.Domain.Outbox;

public enum EmailOutboxMessageStatus
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    Failed = 4
}
