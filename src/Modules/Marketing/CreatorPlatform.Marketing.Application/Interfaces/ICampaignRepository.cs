using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface ICampaignRepository
{
    Task AddAsync(Campaign campaign, CancellationToken ct);

    /// <summary>Tracked — for update/delete/send mutations under the creator's advisory lock.</summary>
    Task<Campaign?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);
}
