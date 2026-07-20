using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface ICampaignRepository
{
    /// <summary>Adds a new send record — campaigns are created directly Queued, never mutated afterward.</summary>
    Task AddAsync(Campaign campaign, CancellationToken ct);

    /// <summary>Read-only lookup — for the detail (history) endpoint.</summary>
    Task<Campaign?> GetByPublicIdAsync(Guid publicId, CancellationToken ct);

    /// <summary>Last <paramref name="take"/> campaigns for a creator, newest first.</summary>
    Task<List<Campaign>> GetRecentByCreatorIdAsync(int creatorId, int take, CancellationToken ct);
}
