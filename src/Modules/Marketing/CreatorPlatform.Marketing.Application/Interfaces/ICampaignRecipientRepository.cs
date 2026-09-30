using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface ICampaignRecipientRepository
{
    /// <summary>Batch insert of the resolved audience snapshot for a queued campaign — one call, not a
    /// loop of single inserts.</summary>
    Task AddRangeAsync(IEnumerable<CampaignRecipient> recipients, CancellationToken ct);

    /// <summary>Stamps <see cref="CampaignRecipient.FirstOpenedAt"/> and bumps the campaign's
    /// <see cref="Campaign.UniqueOpenCount"/> — atomically, in one statement, and only the first time: a
    /// repeat call for an already-opened recipient (or an unknown id) changes nothing.</summary>
    Task RecordFirstOpenAsync(int recipientId, CancellationToken ct);
}
