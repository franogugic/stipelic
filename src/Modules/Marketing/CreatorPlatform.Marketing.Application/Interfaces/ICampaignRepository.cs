using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record CampaignSourceSummary(Guid PublicId, string Subject);

public interface ICampaignRepository
{
    /// <summary>Adds a new send record — Queued and Scheduled campaigns are both created here.</summary>
    Task AddAsync(Campaign campaign, CancellationToken ct);

    /// <summary>Read-only lookup — for the detail (history) endpoint.</summary>
    Task<Campaign?> GetByPublicIdAsync(Guid publicId, CancellationToken ct);

    /// <summary>Tracked — for Scheduled → Queued/Failed/Cancelled transitions (dispatch, cancel).</summary>
    Task<Campaign?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);

    /// <summary>Takes a row lock (<c>SELECT … FOR UPDATE</c>) on the campaign until the transaction ends — the
    /// non-opener resend checks (slots, one pending) run under it on the original.</summary>
    Task LockForUpdateAsync(int campaignId, CancellationToken ct);

    /// <summary>Every non-opener resend of an original (any status), oldest first. Read-only.</summary>
    Task<List<Campaign>> GetResendsAsync(int originalCampaignId, CancellationToken ct);

    /// <summary>Public id and subject of the given campaigns (the originals of resends on a page) — one query.</summary>
    Task<Dictionary<int, CampaignSourceSummary>> GetSourceSummariesAsync(IReadOnlyCollection<int> campaignIds, CancellationToken ct);

    /// <summary>Last <paramref name="take"/> campaigns for a creator, newest first.</summary>
    Task<List<Campaign>> GetRecentByCreatorIdAsync(int creatorId, int take, CancellationToken ct);

    /// <summary>Public ids of Scheduled campaigns whose <c>ScheduledAt</c> has passed, oldest-due-first,
    /// capped at <paramref name="limit"/> — the dispatch worker's poll query.</summary>
    Task<List<Guid>> GetDueScheduledPublicIdsAsync(int limit, CancellationToken ct);
}
