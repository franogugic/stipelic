using CreatorPlatform.Payouts.Domain.Payouts;

namespace CreatorPlatform.Payouts.Application.Interfaces;

public interface IPayoutRepository
{
    Task AddAsync(Payout payout, CancellationToken ct);

    /// <summary>Tracked — for mark-paid/mark-failed transitions.</summary>
    Task<Payout?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct);
}
