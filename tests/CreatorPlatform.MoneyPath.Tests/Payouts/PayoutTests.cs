using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.MoneyPath.Tests.Payouts;

public class PayoutTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_PositiveAmount_StartsAsPending()
    {
        var payout = Payout.Create(creatorId: 1, amountCents: 5000, Currency.Eur, note: null, Now);

        Assert.Equal(PayoutStatus.Pending, payout.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveAmount_Throws(int amountCents)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Payout.Create(1, amountCents, Currency.Eur, null, Now));
    }

    [Fact]
    public void MarkPaid_FromPending_Succeeds()
    {
        var payout = Payout.Create(1, 5000, Currency.Eur, null, Now);

        payout.MarkPaid("REF123", Now.AddMinutes(1));

        Assert.Equal(PayoutStatus.Paid, payout.Status);
        Assert.Equal("REF123", payout.BankReference);
        Assert.NotNull(payout.PaidAt);
    }

    [Fact]
    public void MarkPaid_FromPaid_Throws()
    {
        var payout = Payout.Create(1, 5000, Currency.Eur, null, Now);
        payout.MarkPaid("REF123", Now);

        Assert.Throws<InvalidOperationException>(() => payout.MarkPaid("REF456", Now));
    }

    [Fact]
    public void MarkPaid_FromFailed_Throws()
    {
        var payout = Payout.Create(1, 5000, Currency.Eur, null, Now);
        payout.MarkFailed("didn't work", Now);

        Assert.Throws<InvalidOperationException>(() => payout.MarkPaid("REF", Now));
    }

    [Fact]
    public void MarkFailed_FromPending_Succeeds()
    {
        var payout = Payout.Create(1, 5000, Currency.Eur, null, Now);

        payout.MarkFailed("bad IBAN", Now.AddMinutes(1));

        Assert.Equal(PayoutStatus.Failed, payout.Status);
        Assert.Equal("bad IBAN", payout.Note);
    }

    [Fact]
    public void MarkFailed_FromPaid_Throws()
    {
        var payout = Payout.Create(1, 5000, Currency.Eur, null, Now);
        payout.MarkPaid("REF", Now);

        Assert.Throws<InvalidOperationException>(() => payout.MarkFailed("note", Now));
    }

    [Fact]
    public void MarkFailed_FromFailed_Throws()
    {
        var payout = Payout.Create(1, 5000, Currency.Eur, null, Now);
        payout.MarkFailed("first failure", Now);

        Assert.Throws<InvalidOperationException>(() => payout.MarkFailed("second", Now));
    }
}
