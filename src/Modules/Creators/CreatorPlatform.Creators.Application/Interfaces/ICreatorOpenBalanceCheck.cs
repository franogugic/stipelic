namespace CreatorPlatform.Creators.Application.Interfaces;

/// <summary>Whether a bank-transfer creator is still owed money: a non-zero ledger balance in any currency, or a
/// payout request that is still Pending. Read from the Payouts tables (Creators can't reference Payouts).</summary>
public interface ICreatorOpenBalanceCheck
{
    Task<bool> HasOpenBalanceAsync(int creatorId, CancellationToken ct);
}
