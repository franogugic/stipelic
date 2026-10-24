using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Payouts.Application.Interfaces;

/// <summary>
/// Books BankTransfer ledger entries for a paid order or its refund. Both methods only <c>Add</c> entries
/// to the shared <c>DbContext</c> — neither calls <c>SaveChanges</c>. The caller (e.g. the Orders webhook
/// handler) owns the transaction and must call its own unit of work's <c>SaveChangesAsync</c> afterwards,
/// so the ledger write commits atomically with the order status change.
/// </summary>
public interface IPayoutLedgerService
{
    /// <summary>
    /// Books a <c>SaleCredit</c> for the gross amount and a <c>FeeDebit</c> for the platform fee (skipped
    /// when <paramref name="platformFeeCents"/> is 0). Idempotent: no-ops if a SaleCredit already exists
    /// for this order (webhook retry safe).
    /// </summary>
    Task AppendSaleAsync(
        int creatorId,
        int orderId,
        int amountCents,
        int platformFeeCents,
        Currency currency,
        DateTimeOffset occurredAt,
        CancellationToken ct);

    /// <summary>
    /// Brings the order's refund entries up to <paramref name="refundedToCents"/>, the cumulative refunded amount in
    /// the order's currency: a <c>RefundDebit</c> for what is not yet reversed and a <c>FeeRefundCredit</c> for the
    /// matching share of <paramref name="platformFeeCents"/> (rounded on the cumulative amount, so a full refund gives
    /// back exactly the whole fee). Idempotent: no-ops when the refunded amount is already booked (a replayed or
    /// out-of-order event).
    /// </summary>
    Task AppendRefundAsync(
        int creatorId,
        int orderId,
        int amountCents,
        int platformFeeCents,
        int refundedToCents,
        Currency currency,
        DateTimeOffset occurredAt,
        CancellationToken ct);
}
