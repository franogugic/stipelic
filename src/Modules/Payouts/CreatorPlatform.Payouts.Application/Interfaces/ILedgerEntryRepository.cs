using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Domain.Payouts;

namespace CreatorPlatform.Payouts.Application.Interfaces;

public interface ILedgerEntryRepository
{
    Task AddAsync(LedgerEntry entry, CancellationToken ct);

    /// <summary>Idempotency check — has an entry of this type already been booked for this order?
    /// Used to skip re-booking on webhook retries (the unique index on (OrderId, Type) is the last line of defense).</summary>
    Task<bool> ExistsForOrderAsync(int orderId, LedgerEntryType type, CancellationToken ct);

    /// <summary>Current balance per currency for a creator — one grouped SQL SUM, never summed in application code.</summary>
    Task<List<CreatorBalanceDto>> GetBalanceByCreatorIdAsync(int creatorId, CancellationToken ct);

    /// <summary>BankTransfer creators whose balance is at least <paramref name="minCents"/>, one currency per
    /// row, sorted by balance descending — one grouped SQL SUM joined against creators/payout profiles,
    /// never summed in application code.</summary>
    Task<List<CreatorBalanceSummaryDto>> GetBalancesForPayoutAsync(int minCents, int limit, CancellationToken ct);
}
