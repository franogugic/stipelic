using CreatorPlatform.Marketing.Domain.Mail;

namespace CreatorPlatform.Marketing.Domain.Campaigns;

/// <summary>A record of one send — content is a snapshot (of an <see cref="Templates.EmailTemplate"/> or of
/// content composed inline) taken at the moment of send (editing or archiving the template afterward never
/// changes this row). Created directly as <see cref="CampaignStatus.Queued"/> by
/// <see cref="CreateQueued"/> / <see cref="CreateQueuedFromTemplate"/> inside the
/// send transaction; <see cref="CampaignStatus.Draft"/> remains in the enum only for pre-rework history —
/// nothing constructs one anymore.</summary>
public sealed class Campaign
{
    /// <summary>Non-opener resends allowed per original (owner, 2026-10-22).</summary>
    public const int MaxNonOpenerResends = 5;

    private Campaign()
    {
    }

    private Campaign(
        Guid publicId,
        int creatorId,
        int? templateId,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId,
        CampaignStatus status,
        int recipientCount,
        DateTimeOffset? queuedAt,
        DateTimeOffset? scheduledAt,
        DateTimeOffset createdAt,
        int? sourceCampaignId,
        int? resendRound)
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
        Status = status;
        RecipientCount = recipientCount;
        QueuedAt = queuedAt;
        ScheduledAt = scheduledAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        SourceCampaignId = sourceCampaignId;
        ResendRound = resendRound;
        // An immediate send creates its tracked links in the same transaction.
        ClickTracked = status == CampaignStatus.Queued;
    }

    /// <summary>Snapshots the template's current content (read by the caller just before this call) and
    /// creates the send record directly in <see cref="CampaignStatus.Queued"/> — see
    /// <see cref="CreateQueued"/> for the guards and the no-Draft-step rationale.</summary>
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
        => CreateQueued(
            creatorId, templateId, subject, bodyText, ctaLabel, ctaUrl,
            audienceType, landingPageId, productId, recipientCount, queuedAt);

    /// <summary>Creates the send record directly in <see cref="CampaignStatus.Queued"/> from content handed
    /// in as-is (a template snapshot or content composed inline — either way a snapshot, never a live
    /// link) — there is no Draft step anymore; the send pipeline resolves the audience and consumes the
    /// usage limit before ever calling this, so <paramref name="recipientCount"/> is already known and
    /// final. <paramref name="templateId"/> is only a reference to the template the content came from, if
    /// any. A non-opener resend also carries its original (<paramref name="sourceCampaignId"/>) and its round.</summary>
    public static Campaign CreateQueued(
        int creatorId,
        int? templateId,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId,
        int recipientCount,
        DateTimeOffset queuedAt,
        int? sourceCampaignId = null,
        int? resendRound = null)
    {
        MailContentRules.Validate(subject, bodyText, ctaLabel, ctaUrl);
        ValidateAudience(audienceType, landingPageId, productId, sourceCampaignId, resendRound);

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
            CampaignStatus.Queued,
            recipientCount,
            queuedAt,
            scheduledAt: null,
            createdAt: queuedAt,
            sourceCampaignId,
            resendRound);
    }

    /// <summary>Snapshots the given content (same principle as <see cref="CreateQueued"/>; the content is a
    /// template snapshot or composed inline, <paramref name="templateId"/> is only an optional reference to
    /// the template it came from) but does NOT touch audience/limit/recipients/outbox — the real audience is
    /// resolved again at dispatch time (<see cref="MarkQueuedFromSchedule"/>), never frozen here, so an
    /// unsubscribe between scheduling and dispatch is always honored. <paramref name="recipientCount"/> is unknown until then,
    /// so it's fixed at 0 and deliberately NOT validated positive here (contrast
    /// <see cref="CreateQueued"/> — that guard moves to <see cref="MarkQueuedFromSchedule"/>).</summary>
    public static Campaign CreateScheduled(
        int creatorId,
        int? templateId,
        string subject,
        string bodyText,
        string? ctaLabel,
        string? ctaUrl,
        CampaignAudienceType audienceType,
        int? landingPageId,
        int? productId,
        DateTimeOffset scheduledAt,
        DateTimeOffset createdAt,
        int? sourceCampaignId = null,
        int? resendRound = null)
    {
        MailContentRules.Validate(subject, bodyText, ctaLabel, ctaUrl);
        ValidateAudience(audienceType, landingPageId, productId, sourceCampaignId, resendRound);

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
            CampaignStatus.Scheduled,
            recipientCount: 0,
            queuedAt: null,
            scheduledAt,
            createdAt,
            sourceCampaignId,
            resendRound);
    }

    /// <summary>Dispatch-time transition once the audience has actually been resolved and the usage limit
    /// consumed — guard mirrors <see cref="CreateQueued"/>'s positive-recipient-count check,
    /// moved here since it wasn't knowable at scheduling time.</summary>
    public void MarkQueuedFromSchedule(int recipientCount, DateTimeOffset queuedAt)
    {
        if (Status != CampaignStatus.Scheduled)
            throw new InvalidOperationException($"Cannot mark a {Status} campaign as queued from schedule — only Scheduled campaigns can be.");

        if (recipientCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(recipientCount), recipientCount, "Recipient count must be positive.");

        Status = CampaignStatus.Queued;
        RecipientCount = recipientCount;
        QueuedAt = queuedAt;
        UpdatedAt = queuedAt;
        // The dispatch creates the tracked links, whenever the send was scheduled.
        ClickTracked = true;
    }

    /// <summary>Dispatch of a scheduled send failed (no recipients, limit reached, etc.) — same
    /// Note-on-failure pattern as <see cref="Payouts.Payout.MarkFailed"/>.</summary>
    public void MarkFailed(string? note, DateTimeOffset updatedAt)
    {
        if (Status != CampaignStatus.Scheduled)
            throw new InvalidOperationException($"Cannot mark a {Status} campaign as failed — only Scheduled campaigns can be.");

        Status = CampaignStatus.Failed;
        Note = note;
        UpdatedAt = updatedAt;
    }

    /// <summary>Creator-initiated cancellation of their own still-Scheduled send, before it dispatches.</summary>
    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status != CampaignStatus.Scheduled)
            throw new InvalidOperationException($"Cannot cancel a {Status} campaign — only Scheduled campaigns can be.");

        Status = CampaignStatus.Cancelled;
        UpdatedAt = cancelledAt;
    }

    private static void ValidateAudience(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int? sourceCampaignId, int? resendRound)
    {
        if (audienceType == CampaignAudienceType.CampaignNonOpeners)
        {
            if (sourceCampaignId is null or <= 0 || landingPageId is not null || productId is not null)
                throw new ArgumentException("A CampaignNonOpeners campaign must set SourceCampaignId and leave LandingPageId and ProductId null.");
            if (resendRound is null or < 1 or > MaxNonOpenerResends)
                throw new ArgumentOutOfRangeException(nameof(resendRound), resendRound, $"A resend's round must be between 1 and {MaxNonOpenerResends}.");
            return;
        }

        if (sourceCampaignId is not null || resendRound is not null)
            throw new ArgumentException("Only a CampaignNonOpeners campaign has a source campaign and a resend round.");

        switch (audienceType)
        {
            case CampaignAudienceType.LandingPage:
                if (landingPageId is null or <= 0 || productId is not null)
                    throw new ArgumentException("A LandingPage-audience campaign must set LandingPageId and leave ProductId null.");
                break;
            case CampaignAudienceType.LandingPageBuyers:
                if (landingPageId is null or <= 0 || productId is not null)
                    throw new ArgumentException("A LandingPageBuyers-audience campaign must set LandingPageId and leave ProductId null.");
                break;
            case CampaignAudienceType.Product:
                if (productId is null or <= 0 || landingPageId is not null)
                    throw new ArgumentException("A Product-audience campaign must set ProductId and leave LandingPageId null.");
                break;
            case CampaignAudienceType.All:
                if (landingPageId is not null || productId is not null)
                    throw new ArgumentException("An All-audience campaign must leave both LandingPageId and ProductId null.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(audienceType), audienceType, "Unknown audience type.");
        }
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    /// <summary>Only a reference to the template the content was snapshotted from — null for pre-rework rows
    /// and for sends composed inline without a template.</summary>
    public int? TemplateId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string BodyText { get; private set; } = string.Empty;

    public string? CtaLabel { get; private set; }

    public string? CtaUrl { get; private set; }

    public CampaignAudienceType AudienceType { get; private set; }

    public int? LandingPageId { get; private set; }

    public int? ProductId { get; private set; }

    /// <summary>The original campaign of a non-opener resend (never another resend); null for every other
    /// audience.</summary>
    public int? SourceCampaignId { get; private set; }

    /// <summary>Which resend of its original this is (1–<see cref="MaxNonOpenerResends"/>), fixed when it is
    /// created; null unless <see cref="SourceCampaignId"/> is set.</summary>
    public int? ResendRound { get; private set; }

    public bool IsNonOpenerResend => AudienceType == CampaignAudienceType.CampaignNonOpeners;

    public CampaignStatus Status { get; private set; }

    public int RecipientCount { get; private set; }

    public DateTimeOffset? QueuedAt { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    /// <summary>Recipients whose pixel has loaded at least once — incremented only by the pixel endpoint's
    /// raw SQL, in the same statement that stamps <see cref="CampaignRecipient.FirstOpenedAt"/>, so it
    /// never drifts from the recipient rows.</summary>
    public int UniqueOpenCount { get; private set; }

    /// <summary>Recipients who clicked at least one tracked link — incremented only by the click endpoint's raw SQL,
    /// in the same statement that stamps <see cref="CampaignRecipient.FirstClickedAt"/>.</summary>
    public int UniqueClickCount { get; private set; }

    /// <summary>True once the send created its tracked links (every send since click tracking, F2.1). False for
    /// sends from before it and for a scheduled send not dispatched yet — "not tracked" rather than "no links".</summary>
    public bool ClickTracked { get; private set; }

    /// <summary>Failure reason, set only by <see cref="MarkFailed"/> — null for every other status.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
