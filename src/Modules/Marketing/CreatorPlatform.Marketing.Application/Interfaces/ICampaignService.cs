using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface ICampaignService
{
    Task<AudiencePreviewDto> GetAudiencePreviewAsync(
        string slug, int ownerUserId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct);

    /// <summary>Last 50 campaigns for the creator, each with its outbox send progress — resolved with one
    /// aggregate progress query for the whole page, never one per campaign.</summary>
    Task<List<CampaignListItemDto>> ListAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<CampaignDetailDto> GetAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);

    Task<CampaignDetailDto> CreateAsync(string slug, int ownerUserId, CreateCampaignRequestDto request, CancellationToken ct);

    /// <summary>Draft-only — throws <see cref="Shared.Application.Exceptions.ConflictException"/> once queued.</summary>
    Task<CampaignDetailDto> UpdateAsync(string slug, int ownerUserId, Guid campaignPublicId, UpdateCampaignRequestDto request, CancellationToken ct);

    /// <summary>Draft-only — throws <see cref="Shared.Application.Exceptions.ConflictException"/> once queued.</summary>
    Task DeleteAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);
}
