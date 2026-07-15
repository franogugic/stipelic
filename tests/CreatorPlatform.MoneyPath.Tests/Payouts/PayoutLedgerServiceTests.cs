using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.MoneyPath.Tests.Payouts;

public class PayoutLedgerServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static (PayoutLedgerService Service, FakeLedgerEntryRepository Repository) BuildService()
    {
        var repository = new FakeLedgerEntryRepository();
        var service = new PayoutLedgerService(repository, NullLogger<PayoutLedgerService>.Instance);
        return (service, repository);
    }

    [Fact]
    public async Task AppendSaleAsync_WithFee_BooksSaleCreditAndFeeDebit()
    {
        var (service, repository) = BuildService();

        await service.AppendSaleAsync(creatorId: 1, orderId: 10, amountCents: 1000, platformFeeCents: 50, Currency.Eur, Now, CancellationToken.None);

        Assert.Equal(2, repository.Entries.Count);
        var saleCredit = Assert.Single(repository.Entries, e => e.Type == LedgerEntryType.SaleCredit);
        Assert.Equal(1000, saleCredit.AmountCents);
        var feeDebit = Assert.Single(repository.Entries, e => e.Type == LedgerEntryType.FeeDebit);
        Assert.Equal(-50, feeDebit.AmountCents);
    }

    [Fact]
    public async Task AppendSaleAsync_ZeroFee_BooksOnlySaleCredit()
    {
        var (service, repository) = BuildService();

        await service.AppendSaleAsync(creatorId: 1, orderId: 10, amountCents: 1000, platformFeeCents: 0, Currency.Eur, Now, CancellationToken.None);

        var entry = Assert.Single(repository.Entries);
        Assert.Equal(LedgerEntryType.SaleCredit, entry.Type);
        Assert.Equal(1000, entry.AmountCents);
    }

    [Fact]
    public async Task AppendSaleAsync_AlreadyBooked_IsIdempotent()
    {
        var (service, repository) = BuildService();

        await service.AppendSaleAsync(1, 10, 1000, 50, Currency.Eur, Now, CancellationToken.None);
        await service.AppendSaleAsync(1, 10, 1000, 50, Currency.Eur, Now, CancellationToken.None);

        Assert.Equal(2, repository.Entries.Count);
    }

    [Fact]
    public async Task AppendRefundAsync_WithFee_BooksRefundDebitAndFeeRefundCredit()
    {
        var (service, repository) = BuildService();

        await service.AppendRefundAsync(creatorId: 1, orderId: 10, amountCents: 1000, platformFeeCents: 50, Currency.Eur, Now, CancellationToken.None);

        Assert.Equal(2, repository.Entries.Count);
        var refundDebit = Assert.Single(repository.Entries, e => e.Type == LedgerEntryType.RefundDebit);
        Assert.Equal(-1000, refundDebit.AmountCents);
        var feeRefundCredit = Assert.Single(repository.Entries, e => e.Type == LedgerEntryType.FeeRefundCredit);
        Assert.Equal(50, feeRefundCredit.AmountCents);
    }

    [Fact]
    public async Task AppendRefundAsync_ZeroFee_BooksOnlyRefundDebit()
    {
        var (service, repository) = BuildService();

        await service.AppendRefundAsync(1, 10, 1000, 0, Currency.Eur, Now, CancellationToken.None);

        var entry = Assert.Single(repository.Entries);
        Assert.Equal(LedgerEntryType.RefundDebit, entry.Type);
    }

    [Fact]
    public async Task AppendRefundAsync_AlreadyBooked_IsIdempotent()
    {
        var (service, repository) = BuildService();

        await service.AppendRefundAsync(1, 10, 1000, 50, Currency.Eur, Now, CancellationToken.None);
        await service.AppendRefundAsync(1, 10, 1000, 50, Currency.Eur, Now, CancellationToken.None);

        Assert.Equal(2, repository.Entries.Count);
    }

    [Fact]
    public async Task GetBalanceByCreatorIdAsync_AggregatesByCurrency()
    {
        var (service, repository) = BuildService();

        await service.AppendSaleAsync(1, 10, 1000, 50, Currency.Eur, Now, CancellationToken.None);
        await service.AppendSaleAsync(1, 11, 2000, 100, Currency.Eur, Now, CancellationToken.None);
        await service.AppendSaleAsync(1, 12, 500, 0, Currency.Usd, Now, CancellationToken.None);

        var balances = await repository.GetBalanceByCreatorIdAsync(1, CancellationToken.None);

        var eur = Assert.Single(balances, b => b.Currency == Currency.Eur);
        Assert.Equal(1000 - 50 + 2000 - 100, eur.BalanceCents);
        var usd = Assert.Single(balances, b => b.Currency == Currency.Usd);
        Assert.Equal(500, usd.BalanceCents);
    }
}
