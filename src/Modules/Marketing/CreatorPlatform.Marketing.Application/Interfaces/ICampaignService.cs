using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>Read side of campaigns (send history) — creation happens only via
/// <see cref="ICampaignSendService"/>, there is no Draft CRUD anymore.</summary>
public interface ICampaignService
{
    Task<AudiencePreviewDto> GetAudiencePreviewAsync(
        string slug, int ownerUserId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct);

    /// <summary>Last 50 sends for the creator, each with its outbox send progress — resolved with one
    /// aggregate progress query for the whole page, never one per campaign.</summary>
    Task<List<CampaignListItemDto>> ListAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<CampaignDetailDto> GetAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);
}
