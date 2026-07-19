namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed record CampaignListItemDto(
    Guid PublicId,
    string Subject,
    string Status,
    string AudienceType,
    Guid TargetPublicId,
    int RecipientCount,
    DateTimeOffset? QueuedAt,
    DateTimeOffset CreatedAt,
    int SentCount,
    int FailedCount);
