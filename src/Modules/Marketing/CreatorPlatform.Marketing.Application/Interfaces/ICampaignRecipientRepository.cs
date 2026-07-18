using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface ICampaignRecipientRepository
{
    /// <summary>Batch insert of the resolved audience snapshot for a queued campaign — one call, not a
    /// loop of single inserts.</summary>
    Task AddRangeAsync(IEnumerable<CampaignRecipient> recipients, CancellationToken ct);
}
