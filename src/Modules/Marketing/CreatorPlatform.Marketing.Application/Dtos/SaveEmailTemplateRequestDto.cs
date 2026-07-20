namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed class SaveEmailTemplateRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string BodyText { get; init; } = string.Empty;
    public string? CtaLabel { get; init; }
    public string? CtaUrl { get; init; }
}
