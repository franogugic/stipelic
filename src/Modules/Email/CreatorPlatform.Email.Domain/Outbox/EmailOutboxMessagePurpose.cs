namespace CreatorPlatform.Email.Domain.Outbox;

public enum EmailOutboxMessagePurpose
{
    EmailVerification = 1,
    OrderAccess = 2,
    PayoutRequested = 3
}
