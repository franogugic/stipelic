namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed class SendCampaignRequestDto
{
    public Guid TemplatePublicId { get; init; }
    public string AudienceType { get; init; } = string.Empty;
    public Guid TargetPublicId { get; init; }

    /// <summary>Null = send immediately (unchanged behavior). Set = schedule for later; must be at least
    /// 2 minutes and at most 365 days from now, validated server-side regardless of any client-side check.</summary>
    public DateTimeOffset? ScheduledAt { get; init; }
}
