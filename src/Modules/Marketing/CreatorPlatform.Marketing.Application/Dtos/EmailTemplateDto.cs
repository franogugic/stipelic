namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed record EmailTemplateDto(
    Guid PublicId,
    string Name,
    string Subject,
    string BodyText,
    string? CtaLabel,
    string? CtaUrl,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
