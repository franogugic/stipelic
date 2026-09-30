namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed class SendCampaignRequestDto
{
    /// <summary>Template to send. Optional when the content below is composed inline; when both are given the
    /// content is what gets sent and the template is kept only as a reference.</summary>
    public Guid? TemplatePublicId { get; init; }

    /// <summary>Inline content — Subject and BodyText are both required as soon as any of the four content
    /// fields is set; CtaLabel and CtaUrl are both-or-neither (see <c>MailContentRules</c>).</summary>
    public string? Subject { get; init; }
    public string? BodyText { get; init; }
    public string? CtaLabel { get; init; }
    public string? CtaUrl { get; init; }

    public string AudienceType { get; init; } = string.Empty;

    /// <summary>Landing page / product public id; must be empty for the <c>All</c> audience.</summary>
    public Guid TargetPublicId { get; init; }

    /// <summary>Null = send immediately (unchanged behavior). Set = schedule for later; must be at least
    /// 2 minutes and at most 365 days from now, validated server-side regardless of any client-side check.</summary>
    public DateTimeOffset? ScheduledAt { get; init; }
}
