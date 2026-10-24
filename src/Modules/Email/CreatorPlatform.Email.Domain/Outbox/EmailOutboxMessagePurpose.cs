namespace CreatorPlatform.Email.Domain.Outbox;

public enum EmailOutboxMessagePurpose
{
    EmailVerification = 1,
    OrderAccess = 2,
    CampaignBroadcast = 4,
    PasswordReset = 5,
    EmailChangeVerification = 6,
    EmailChanged = 7,
    LeadMagnetDelivery = 8
}
