using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>One-shot fan-out of a Draft campaign into the email outbox. See implementation for the exact
/// transactional flow (TASKS-02-EMAIL-MARKETING.md Task 4 point 3).</summary>
public interface ICampaignSendService
{
    Task<CampaignDetailDto> SendAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);
}
