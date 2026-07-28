using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>Read side of campaigns (send history) — creation happens only via
/// <see cref="ICampaignSendService"/>, there is no Draft CRUD anymore.</summary>
public interface ICampaignService
{
    Task<AudiencePreviewDto> GetAudiencePreviewAsync(
        string slug, int ownerUserId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct);

    /// <summary>Keyset page of the actual audience emails for a not-yet-sent target — same ownership
    /// resolution as <see cref="GetAudiencePreviewAsync"/>, paged 10/50-ish like Contacts, never OFFSET.</summary>
    Task<AudienceRecipientsPageDto> GetAudienceRecipientsAsync(
        string slug, int ownerUserId, CampaignAudienceType audienceType, Guid targetPublicId,
        string? afterEmail, int limit, CancellationToken ct);

    /// <summary>Last 50 sends for the creator, each with its outbox send progress — resolved with one
    /// aggregate progress query for the whole page, never one per campaign.</summary>
    Task<List<CampaignListItemDto>> ListAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<CampaignDetailDto> GetAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);

    /// <summary>Recipients whose delivery terminally failed for this send, with their last error —
    /// ownership-checked the same way as <see cref="GetAsync"/>.</summary>
    Task<List<FailedRecipientDto>> GetFailedRecipientsAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);
}
