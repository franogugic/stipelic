namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed class UpdateCampaignRequestDto
{
    public string Subject { get; init; } = string.Empty;
    public string BodyText { get; init; } = string.Empty;
    public string? CtaLabel { get; init; }
    public string? CtaUrl { get; init; }
    public string AudienceType { get; init; } = string.Empty;
    public Guid TargetPublicId { get; init; }
}
