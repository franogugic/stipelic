namespace CreatorPlatform.Marketing.Domain.Campaigns;

/// <summary>One recipient snapshot for a queued campaign — who the audience resolved to at send time.
/// Delivery status is NOT duplicated here; it lives in the email outbox, correlated via
/// <c>{campaignPublicId}:{recipientId}</c> (see Marketing send pipeline, Task 4).</summary>
public sealed class CampaignRecipient
{
    private CampaignRecipient()
    {
    }

    private CampaignRecipient(int campaignId, string email, DateTimeOffset createdAt)
    {
        CampaignId = campaignId;
        Email = email;
        CreatedAt = createdAt;
    }

    public static CampaignRecipient Create(int campaignId, string email, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return new CampaignRecipient(campaignId, email.Trim().ToLowerInvariant(), createdAt);
    }

    public int Id { get; private set; }

    public int CampaignId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}
