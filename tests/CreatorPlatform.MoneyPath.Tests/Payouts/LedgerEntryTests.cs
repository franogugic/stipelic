using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.MoneyPath.Tests.Payouts;

public class LedgerEntryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateSaleCredit_PositiveAmount_Succeeds()
    {
        var entry = LedgerEntry.CreateSaleCredit(creatorId: 1, orderId: 10, amountCents: 500, Currency.Eur, Now);

        Assert.Equal(LedgerEntryType.SaleCredit, entry.Type);
        Assert.Equal(500, entry.AmountCents);
        Assert.Equal(10, entry.OrderId);
        Assert.Null(entry.PayoutId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateSaleCredit_NonPositiveAmount_Throws(int amountCents)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerEntry.CreateSaleCredit(1, 10, amountCents, Currency.Eur, Now));
    }

    [Fact]
    public void CreateFeeDebit_NegativeAmount_Succeeds()
    {
        var entry = LedgerEntry.CreateFeeDebit(1, orderId: 10, amountCents: -50, Currency.Eur, Now);

        Assert.Equal(LedgerEntryType.FeeDebit, entry.Type);
        Assert.Equal(-50, entry.AmountCents);
        Assert.Equal(10, entry.OrderId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void CreateFeeDebit_NonNegativeAmount_Throws(int amountCents)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerEntry.CreateFeeDebit(1, 10, amountCents, Currency.Eur, Now));
    }

    [Fact]
    public void CreateRefundDebit_NegativeAmount_Succeeds()
    {
        var entry = LedgerEntry.CreateRefundDebit(1, orderId: 10, amountCents: -500, Currency.Eur, Now);

        Assert.Equal(LedgerEntryType.RefundDebit, entry.Type);
        Assert.Equal(10, entry.OrderId);
    }

    [Fact]
    public void CreateRefundDebit_PositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerEntry.CreateRefundDebit(1, 10, 500, Currency.Eur, Now));
    }

    [Fact]
    public void CreateFeeRefundCredit_PositiveAmount_Succeeds()
    {
        var entry = LedgerEntry.CreateFeeRefundCredit(1, orderId: 10, amountCents: 50, Currency.Eur, Now);

        Assert.Equal(LedgerEntryType.FeeRefundCredit, entry.Type);
        Assert.Equal(10, entry.OrderId);
    }

    [Fact]
    public void CreateFeeRefundCredit_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerEntry.CreateFeeRefundCredit(1, 10, -50, Currency.Eur, Now));
    }

    [Fact]
    public void CreatePayoutDebit_NegativeAmount_Succeeds()
    {
        var entry = LedgerEntry.CreatePayoutDebit(1, payoutId: 99, amountCents: -1000, Currency.Eur, Now);

        Assert.Equal(LedgerEntryType.PayoutDebit, entry.Type);
        Assert.Equal(99, entry.PayoutId);
        Assert.Null(entry.OrderId);
    }

    [Fact]
    public void CreatePayoutDebit_PositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerEntry.CreatePayoutDebit(1, 99, 1000, Currency.Eur, Now));
    }

    [Fact]
    public void CreateAdjustment_PositiveAmount_Succeeds()
    {
        var entry = LedgerEntry.CreateAdjustment(1, payoutId: 99, amountCents: 1000, Currency.Eur, Now);

        Assert.Equal(LedgerEntryType.Adjustment, entry.Type);
        Assert.Equal(99, entry.PayoutId);
        Assert.Null(entry.OrderId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateAdjustment_NonPositiveAmount_Throws(int amountCents)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerEntry.CreateAdjustment(1, 99, amountCents, Currency.Eur, Now));
    }
}
