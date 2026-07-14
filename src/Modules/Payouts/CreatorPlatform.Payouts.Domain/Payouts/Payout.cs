using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Payouts.Domain.Payouts;

/// <summary>A record of one bank-transfer payout to a BankTransfer-mode creator. The actual money
/// movement happens manually (internet banking) — this is the system's record of what was decided/sent.</summary>
public sealed class Payout
{
    private Payout()
    {
    }

    private Payout(
        Guid publicId,
        int creatorId,
        int amountCents,
        Currency currency,
        string? note,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        AmountCents = amountCents;
        Currency = currency;
        Status = PayoutStatus.Pending;
        Note = note;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Payout Create(int creatorId, int amountCents, Currency currency, string? note, DateTimeOffset createdAt)
    {
        if (amountCents <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountCents), amountCents, "Payout amount must be positive.");

        return new Payout(Guid.NewGuid(), creatorId, amountCents, currency, note, createdAt);
    }

    public void MarkPaid(string bankReference, DateTimeOffset paidAt)
    {
        if (Status != PayoutStatus.Pending)
            throw new InvalidOperationException($"Cannot mark a {Status} payout as paid — only Pending payouts can be.");

        Status = PayoutStatus.Paid;
        BankReference = bankReference;
        PaidAt = paidAt;
        UpdatedAt = paidAt;
    }

    public void MarkFailed(string? note, DateTimeOffset updatedAt)
    {
        if (Status != PayoutStatus.Pending)
            throw new InvalidOperationException($"Cannot mark a {Status} payout as failed — only Pending payouts can be.");

        Status = PayoutStatus.Failed;
        Note = note;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public int AmountCents { get; private set; }

    public Currency Currency { get; private set; }

    public PayoutStatus Status { get; private set; }

    public string? BankReference { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
