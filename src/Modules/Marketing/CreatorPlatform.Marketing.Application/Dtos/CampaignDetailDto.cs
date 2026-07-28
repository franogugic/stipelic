namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed record CampaignDetailDto(
    Guid PublicId,
    string Subject,
    string BodyText,
    string? CtaLabel,
    string? CtaUrl,
    string AudienceType,
    Guid TargetPublicId,
    string Status,
    int RecipientCount,
    DateTimeOffset? QueuedAt,
    DateTimeOffset? ScheduledAt,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int SentCount,
    int FailedCount);
