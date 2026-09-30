using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>Only the fields the open rate trend needs — deliberately not the whole <see cref="Campaign"/>,
/// whose body text can be up to 10,000 characters.</summary>
public sealed record QueuedCampaignOpenStats(Guid PublicId, DateTimeOffset QueuedAt, int UniqueOpenCount);

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

    /// <summary>Read-only: the creator's campaigns queued at or after <paramref name="since"/> (so Scheduled,
    /// Cancelled and undispatched-Failed ones, which have no <c>QueuedAt</c>, are never included) — feeds
    /// the open rate trend.</summary>
    Task<List<QueuedCampaignOpenStats>> GetQueuedSinceAsync(int creatorId, DateTimeOffset since, CancellationToken ct);

    /// <summary>Public ids of Scheduled campaigns whose <c>ScheduledAt</c> has passed, oldest-due-first,
    /// capped at <paramref name="limit"/> — the dispatch worker's poll query.</summary>
    Task<List<Guid>> GetDueScheduledPublicIdsAsync(int limit, CancellationToken ct);
}
