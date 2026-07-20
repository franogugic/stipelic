namespace CreatorPlatform.Marketing.Application.Dtos;

public sealed class SendCampaignRequestDto
{
    public Guid TemplatePublicId { get; init; }
    public string AudienceType { get; init; } = string.Empty;
    public Guid TargetPublicId { get; init; }
}
