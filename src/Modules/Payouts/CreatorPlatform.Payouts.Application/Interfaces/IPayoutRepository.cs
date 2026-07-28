using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Domain.Payouts;

namespace CreatorPlatform.Payouts.Application.Interfaces;

public interface IPayoutRepository
{
    Task AddAsync(Payout payout, CancellationToken ct);

    /// <summary>Admin queue: payouts joined with creator name/slug and full (unmasked) bank details, one
    /// query, no N+1. Pending is ordered oldest-first (FIFO processing); every other status/filter is
    /// ordered newest-first.</summary>
    Task<List<AdminPayoutQueueItemDto>> ListForQueueAsync(PayoutStatus? status, int limit, CancellationToken ct);

    /// <summary>Tracked — for mark-paid/mark-failed transitions.</summary>
    Task<Payout?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);

    /// <summary>Sum of all Pending payout amounts for a creator — one SQL SUM, never summed in application code.</summary>
    Task<int> GetPendingAmountCentsByCreatorIdAsync(int creatorId, CancellationToken ct);

    /// <summary>App-level guard against a second concurrent request — checked under the creator's
    /// advisory lock. The partial unique index on (CreatorId) WHERE Status = 'Pending' is the DB-level
    /// backstop for the same rule.</summary>
    Task<bool> HasPendingPayoutAsync(int creatorId, CancellationToken ct);

    /// <summary>Most recent payouts for a creator, newest first, capped at <paramref name="limit"/>.</summary>
    Task<List<PayoutDto>> ListRecentByCreatorIdAsync(int creatorId, int limit, CancellationToken ct);
}
