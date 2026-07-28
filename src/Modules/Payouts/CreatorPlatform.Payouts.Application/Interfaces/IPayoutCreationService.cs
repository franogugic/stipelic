using CreatorPlatform.Payouts.Domain.Payouts;

namespace CreatorPlatform.Payouts.Application.Interfaces;

/// <summary>
/// Core payout-creation money path, shared by admin-initiated and creator self-serve requests so the
/// advisory lock / balance recheck / guard logic exists in exactly one place. Under the creator's
/// <c>pg_advisory_xact_lock</c>: max-one-Pending-request guard, balance recheck, insert
/// <c>Payout(Pending)</c> + <c>PayoutDebit</c> ledger entry — all in one transaction.
/// </summary>
public interface IPayoutCreationService
{
    /// <param name="amountCents">Null means "the creator's full available balance at lock time" — used
    /// by the creator self-serve "request full balance" option. Admin-initiated payouts always pass an
    /// explicit amount.</param>
    /// <param name="onCreatedInTransaction">Optional side effect run inside the same transaction right
    /// after the payout and ledger entry are added (e.g. queuing the admin notification email) — so it
    /// commits or rolls back atomically with the payout itself.</param>
    Task<Payout> CreatePayoutAsync(
        CreatorPayoutContext creatorContext,
        int? amountCents,
        string? note,
        Func<Payout, Task>? onCreatedInTransaction,
        CancellationToken ct);
}
