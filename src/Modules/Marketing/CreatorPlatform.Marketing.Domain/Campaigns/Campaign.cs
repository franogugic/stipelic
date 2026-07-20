using CreatorPlatform.Marketing.Domain.Mail;

namespace CreatorPlatform.Marketing.Domain.Campaigns;

/// <summary>A record of one send — content is a snapshot of an <see cref="Templates.EmailTemplate"/> taken
/// at the moment of send (editing or archiving the template afterward never changes this row). Created
/// directly as <see cref="CampaignStatus.Queued"/> by <see cref="CreateQueuedFromTemplate"/> inside the
/// send transaction; <see cref="CampaignStatus.Draft"/> remains in the enum only for pre-rework history —
/// nothing constructs one anymore.</summary>
public sealed class Campaign
{
    private Campaign()
    {
    }

    private Campaign(
        Guid publicId,
        int creatorId,
        int templateId,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId,
        int recipientCount,
        DateTimeOffset queuedAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        TemplateId = templateId;
        Subject = subject;
        BodyText = bodyText;
        CtaLabel = ctaLabel;
        CtaUrl = ctaUrl;
        AudienceType = audienceType;
        LandingPageId = landingPageId;
        ProductId = productId;
        Status = CampaignStatus.Queued;
        RecipientCount = recipientCount;
        QueuedAt = queuedAt;
        CreatedAt = queuedAt;
        UpdatedAt = queuedAt;
    }

    /// <summary>Snapshots <paramref name="subject"/>/<paramref name="bodyText"/>/CTA (the template's
    /// current content, read by the caller just before this call) and creates the send record directly in
    /// <see cref="CampaignStatus.Queued"/> — there is no Draft step anymore; the send pipeline resolves
    /// the audience and consumes the usage limit before ever calling this, so <paramref name="recipientCount"/>
    /// is already known and final.</summary>
    public static Campaign CreateQueuedFromTemplate(
        int creatorId,
        int templateId,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId,
        int recipientCount,
        DateTimeOffset queuedAt)
    {
        MailContentRules.Validate(subject, bodyText, ctaLabel, ctaUrl);
        ValidateAudience(audienceType, landingPageId, productId);

        if (recipientCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(recipientCount), recipientCount, "Recipient count must be positive.");

        return new Campaign(
            Guid.NewGuid(),
            creatorId,
            templateId,
            subject.Trim(),
            bodyText,
            ctaLabel?.Trim(),
            ctaUrl?.Trim(),
            audienceType,
            landingPageId,
            productId,
            recipientCount,
            queuedAt);
    }

    private static void ValidateAudience(CampaignAudienceType audienceType, int? landingPageId, int? productId)
    {
        switch (audienceType)
        {
            case CampaignAudienceType.LandingPage:
                if (landingPageId is null or <= 0 || productId is not null)
                    throw new ArgumentException("A LandingPage-audience campaign must set LandingPageId and leave ProductId null.");
                break;
            case CampaignAudienceType.Product:
                if (productId is null or <= 0 || landingPageId is not null)
                    throw new ArgumentException("A Product-audience campaign must set ProductId and leave LandingPageId null.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(audienceType), audienceType, "Unknown audience type.");
        }
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    /// <summary>Null only for pre-rework rows sent before templates existed ("legacy inline send").</summary>
    public int? TemplateId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string BodyText { get; private set; } = string.Empty;

    public string? CtaLabel { get; private set; }

    public string? CtaUrl { get; private set; }

    public CampaignAudienceType AudienceType { get; private set; }

    public int? LandingPageId { get; private set; }

    public int? ProductId { get; private set; }

    public CampaignStatus Status { get; private set; }

    public int RecipientCount { get; private set; }

    public DateTimeOffset? QueuedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
