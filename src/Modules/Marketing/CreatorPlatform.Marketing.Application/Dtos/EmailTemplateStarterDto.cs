namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed record EmailTemplateStarterDto(
    string Key,
    string Name,
    string Description,
    string Subject,
    string BodyText,
    string? CtaLabel,
    string? CtaUrl);
