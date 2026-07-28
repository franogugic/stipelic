namespace CreatorPlatform.Email.Domain.Outbox;

public enum EmailOutboxMessagePurpose
{
    EmailVerification = 1,
    OrderAccess = 2,
    PayoutRequested = 3,
    CampaignBroadcast = 4,
    PasswordReset = 5
}
