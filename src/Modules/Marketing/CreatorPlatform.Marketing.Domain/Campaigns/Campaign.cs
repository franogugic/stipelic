namespace CreatorPlatform.Marketing.Domain.Campaigns;

/// <summary>A promotional email campaign sent by a creator to an audience of their own captured emails
/// (one landing page, or the union of a product's landing pages). Draft until <see cref="MarkQueued"/>,
/// after which it's immutable (send is a one-shot fan-out into the email outbox — see Marketing.Application
/// Task 4's send pipeline).</summary>
public sealed class Campaign
{
    public const int MaxSubjectLength = 200;
    public const int MaxBodyTextLength = 10_000;
    public const int MaxCtaLabelLength = 100;
    public const int MaxCtaUrlLength = 2000;

    private Campaign()
    {
    }

    private Campaign(
        Guid publicId,
        int creatorId,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        Subject = subject;
        BodyText = bodyText;
        CtaLabel = ctaLabel;
        CtaUrl = ctaUrl;
        AudienceType = audienceType;
        LandingPageId = landingPageId;
        ProductId = productId;
        Status = CampaignStatus.Draft;
        RecipientCount = 0;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Campaign CreateDraft(
        int creatorId,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId,
        DateTimeOffset createdAt)
    {
        ValidateFields(subject, bodyText, ctaLabel, ctaUrl, audienceType, landingPageId, productId);

        return new Campaign(
            Guid.NewGuid(),
            creatorId,
            subject.Trim(),
            bodyText,
            ctaLabel?.Trim(),
            ctaUrl?.Trim(),
            audienceType,
            landingPageId,
            productId,
            createdAt);
    }

    public void UpdateDraft(
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId,
        DateTimeOffset updatedAt)
    {
        if (Status != CampaignStatus.Draft)
            throw new InvalidOperationException($"Cannot update a {Status} campaign — only Draft campaigns can be edited.");

        ValidateFields(subject, bodyText, ctaLabel, ctaUrl, audienceType, landingPageId, productId);

        Subject = subject.Trim();
        BodyText = bodyText;
        CtaLabel = ctaLabel?.Trim();
        CtaUrl = ctaUrl?.Trim();
        AudienceType = audienceType;
        LandingPageId = landingPageId;
        ProductId = productId;
        UpdatedAt = updatedAt;
    }

    /// <summary>Transitions Draft -> Queued once the send pipeline has resolved the audience and is
    /// about to fan out into the outbox. <paramref name="recipientCount"/> is a snapshot, not a live
    /// count — it does not change afterward even if the underlying audience later shrinks (e.g. someone
    /// unsubscribes after the send).</summary>
    public void MarkQueued(int recipientCount, DateTimeOffset queuedAt)
    {
        if (Status != CampaignStatus.Draft)
            throw new InvalidOperationException($"Cannot queue a {Status} campaign — only Draft campaigns can be queued.");

        if (recipientCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(recipientCount), recipientCount, "Recipient count must be positive.");

        Status = CampaignStatus.Queued;
        RecipientCount = recipientCount;
        QueuedAt = queuedAt;
        UpdatedAt = queuedAt;
    }

    private static void ValidateFields(
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (subject.Trim().Length > MaxSubjectLength)
            throw new ArgumentException($"Subject must be at most {MaxSubjectLength} characters.", nameof(subject));

        if (string.IsNullOrWhiteSpace(bodyText))
            throw new ArgumentException("Body text is required.", nameof(bodyText));
        if (bodyText.Length > MaxBodyTextLength)
            throw new ArgumentException($"Body text must be at most {MaxBodyTextLength} characters.", nameof(bodyText));

        var hasCtaLabel = !string.IsNullOrWhiteSpace(ctaLabel);
        var hasCtaUrl = !string.IsNullOrWhiteSpace(ctaUrl);
        if (hasCtaLabel != hasCtaUrl)
            throw new ArgumentException("CTA label and URL must both be set, or both be empty.");
        if (hasCtaLabel && ctaLabel!.Trim().Length > MaxCtaLabelLength)
            throw new ArgumentException($"CTA label must be at most {MaxCtaLabelLength} characters.", nameof(ctaLabel));
        if (hasCtaUrl && ctaUrl!.Trim().Length > MaxCtaUrlLength)
            throw new ArgumentException($"CTA URL must be at most {MaxCtaUrlLength} characters.", nameof(ctaUrl));

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
