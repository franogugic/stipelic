using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface ICampaignLinkRepository
{
    Task AddRangeAsync(IEnumerable<CampaignLink> links, CancellationToken ct);

    /// <summary>A campaign's links in position order. Read-only.</summary>
    Task<List<CampaignLink>> GetByCampaignIdAsync(int campaignId, CancellationToken ct);

    /// <summary>How many links each campaign has (missing = 0) — one aggregate query for a list page.</summary>
    Task<Dictionary<int, int>> GetLinkCountsAsync(IReadOnlyCollection<int> campaignIds, CancellationToken ct);

    /// <summary>The destination of <paramref name="linkId"/>, but only if it is a link of the campaign
    /// <paramref name="recipientId"/> received; null otherwise.</summary>
    Task<string?> GetTargetUrlAsync(int recipientId, int linkId, CancellationToken ct);

    /// <summary>Records one click in one statement: upserts the (link, recipient) row and bumps its count; on the
    /// recipient's first click on this link bumps the link's unique count; on the recipient's first click on any
    /// link of the campaign stamps <see cref="CampaignRecipient.FirstClickedAt"/> and bumps
    /// <see cref="Campaign.UniqueClickCount"/>. Does nothing when the link is not in the recipient's campaign.</summary>
    Task RecordClickAsync(int recipientId, int linkId, CancellationToken ct);
}
