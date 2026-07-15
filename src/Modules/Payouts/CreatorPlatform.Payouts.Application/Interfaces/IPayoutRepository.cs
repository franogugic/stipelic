using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Domain.Payouts;

namespace CreatorPlatform.Payouts.Application.Interfaces;

public interface IPayoutRepository
{
    Task AddAsync(Payout payout, CancellationToken ct);

    /// <summary>Tracked — for mark-paid/mark-failed transitions.</summary>
    Task<Payout?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);

    /// <summary>Sum of all Pending payout amounts for a creator — one SQL SUM, never summed in application code.</summary>
    Task<int> GetPendingAmountCentsByCreatorIdAsync(int creatorId, CancellationToken ct);

    /// <summary>Most recent payouts for a creator, newest first, capped at <paramref name="limit"/>.</summary>
    Task<List<PayoutDto>> ListRecentByCreatorIdAsync(int creatorId, int limit, CancellationToken ct);
}
