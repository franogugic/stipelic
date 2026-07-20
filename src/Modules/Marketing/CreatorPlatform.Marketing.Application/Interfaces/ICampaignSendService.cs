using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>Sends an Active template to an audience, one shot — snapshots the template's current content
/// into a new Queued <see cref="Campaigns.Campaign"/> row and fans out into the email outbox. See
/// implementation for the exact transactional flow.</summary>
public interface ICampaignSendService
{
    Task<CampaignDetailDto> SendAsync(string slug, int ownerUserId, SendCampaignRequestDto request, CancellationToken ct);
}
