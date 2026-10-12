using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface ICampaignRepository
{
    /// <summary>Adds a new send record — Queued and Scheduled campaigns are both created here.</summary>
    Task AddAsync(Campaign campaign, CancellationToken ct);

    /// <summary>Read-only lookup — for the detail (history) endpoint.</summary>
    Task<Campaign?> GetByPublicIdAsync(Guid publicId, CancellationToken ct);

    /// <summary>Tracked — for Scheduled → Queued/Failed/Cancelled transitions (dispatch, cancel).</summary>
    Task<Campaign?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);

    /// <summary>Last <paramref name="take"/> campaigns for a creator, newest first.</summary>
    Task<List<Campaign>> GetRecentByCreatorIdAsync(int creatorId, int take, CancellationToken ct);

    /// <summary>Public ids of Scheduled campaigns whose <c>ScheduledAt</c> has passed, oldest-due-first,
    /// capped at <paramref name="limit"/> — the dispatch worker's poll query.</summary>
    Task<List<Guid>> GetDueScheduledPublicIdsAsync(int limit, CancellationToken ct);
}
