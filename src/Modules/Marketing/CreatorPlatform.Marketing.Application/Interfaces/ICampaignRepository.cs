using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface ICampaignRepository
{
    Task AddAsync(Campaign campaign, CancellationToken ct);

    /// <summary>Tracked — for update/delete/send mutations under the creator's advisory lock.</summary>
    Task<Campaign?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);

    /// <summary>Read-only lookup for the detail endpoint — not tracked, no lock needed.</summary>
    Task<Campaign?> GetByPublicIdAsync(Guid publicId, CancellationToken ct);

    /// <summary>Last <paramref name="take"/> campaigns for a creator, newest first.</summary>
    Task<List<Campaign>> GetRecentByCreatorIdAsync(int creatorId, int take, CancellationToken ct);

    void Remove(Campaign campaign);
}
