namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed record EmailTemplateStarterDto(
    string Key,
    string Name,
    string Subject,
    string BodyText,
    string? CtaLabel,
    string? CtaUrl);
