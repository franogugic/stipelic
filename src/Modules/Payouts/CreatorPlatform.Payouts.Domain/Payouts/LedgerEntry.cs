using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Payouts.Domain.Payouts;

/// <summary>
/// Append-only bookkeeping row for a creator's BankTransfer balance. Never updated or deleted after
/// insert — corrections are made by inserting a new, opposite-signed entry (see <see cref="CreateAdjustment"/>).
/// </summary>
public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    private LedgerEntry(
        Guid publicId,
        int creatorId,
        int? orderId,
        int? payoutId,
        LedgerEntryType type,
        int amountCents,
        Currency currency,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        OrderId = orderId;
        PayoutId = payoutId;
        Type = type;
        AmountCents = amountCents;
        Currency = currency;
        CreatedAt = createdAt;
    }

    /// <summary>Order paid — credits the creator the full gross sale amount. Requires <paramref name="amountCents"/> &gt; 0.</summary>
    public static LedgerEntry CreateSaleCredit(int creatorId, int orderId, int amountCents, Currency currency, DateTimeOffset createdAt)
    {
        RequirePositive(amountCents, nameof(amountCents));
        return new LedgerEntry(Guid.NewGuid(), creatorId, orderId, null, LedgerEntryType.SaleCredit, amountCents, currency, createdAt);
    }

    /// <summary>Platform's cut of an order — debits the creator. Requires <paramref name="amountCents"/> &lt; 0.</summary>
    public static LedgerEntry CreateFeeDebit(int creatorId, int orderId, int amountCents, Currency currency, DateTimeOffset createdAt)
    {
        RequireNegative(amountCents, nameof(amountCents));
        return new LedgerEntry(Guid.NewGuid(), creatorId, orderId, null, LedgerEntryType.FeeDebit, amountCents, currency, createdAt);
    }

    /// <summary>Order refunded — reverses the sale credit. Requires <paramref name="amountCents"/> &lt; 0.</summary>
    public static LedgerEntry CreateRefundDebit(int creatorId, int orderId, int amountCents, Currency currency, DateTimeOffset createdAt)
    {
        RequireNegative(amountCents, nameof(amountCents));
        return new LedgerEntry(Guid.NewGuid(), creatorId, orderId, null, LedgerEntryType.RefundDebit, amountCents, currency, createdAt);
    }

    /// <summary>Order refunded — reverses the platform fee debit back to the creator. Requires <paramref name="amountCents"/> &gt; 0.</summary>
    public static LedgerEntry CreateFeeRefundCredit(int creatorId, int orderId, int amountCents, Currency currency, DateTimeOffset createdAt)
    {
        RequirePositive(amountCents, nameof(amountCents));
        return new LedgerEntry(Guid.NewGuid(), creatorId, orderId, null, LedgerEntryType.FeeRefundCredit, amountCents, currency, createdAt);
    }

    /// <summary>Money sent out to the creator's bank account. Requires <paramref name="amountCents"/> &lt; 0.</summary>
    public static LedgerEntry CreatePayoutDebit(int creatorId, int payoutId, int amountCents, Currency currency, DateTimeOffset createdAt)
    {
        RequireNegative(amountCents, nameof(amountCents));
        return new LedgerEntry(Guid.NewGuid(), creatorId, null, payoutId, LedgerEntryType.PayoutDebit, amountCents, currency, createdAt);
    }

    /// <summary>
    /// Compensating credit — e.g. reverses a <see cref="PayoutDebit"/> when a payout is marked failed.
    /// Requires <paramref name="amountCents"/> &gt; 0 (this factory only models the credit case).
    /// </summary>
    public static LedgerEntry CreateAdjustment(int creatorId, int payoutId, int amountCents, Currency currency, DateTimeOffset createdAt)
    {
        RequirePositive(amountCents, nameof(amountCents));
        return new LedgerEntry(Guid.NewGuid(), creatorId, null, payoutId, LedgerEntryType.Adjustment, amountCents, currency, createdAt);
    }

    private static void RequirePositive(int amountCents, string paramName)
    {
        if (amountCents <= 0)
            throw new ArgumentOutOfRangeException(paramName, amountCents, "Credit ledger entries must have a positive amount.");
    }

    private static void RequireNegative(int amountCents, string paramName)
    {
        if (amountCents >= 0)
            throw new ArgumentOutOfRangeException(paramName, amountCents, "Debit ledger entries must have a negative amount.");
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public int? OrderId { get; private set; }

    public int? PayoutId { get; private set; }

    public LedgerEntryType Type { get; private set; }

    /// <summary>Signed: credits positive, debits negative.</summary>
    public int AmountCents { get; private set; }

    public Currency Currency { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
