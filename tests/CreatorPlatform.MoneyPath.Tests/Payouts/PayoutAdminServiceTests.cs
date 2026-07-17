using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Options;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Payouts;

public class PayoutAdminServiceTests
{
    private static readonly Guid CreatorPublicId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private const int CreatorId = 1;

    private static CreatorPayoutContext BuildContext(
        PayoutMode payoutMode = PayoutMode.BankTransfer,
        bool hasPayoutProfile = true,
        Currency currency = Currency.Eur) => new(
        CreatorId,
        CreatorPublicId,
        "Acme",
        "acme",
        CreatorStatus.Active,
        payoutMode,
        currency,
        hasPayoutProfile);

    private static (PayoutAdminService Service, FakeLedgerEntryRepository LedgerRepository, FakePayoutRepository PayoutRepository, FakePayoutsUnitOfWork UnitOfWork)
        BuildService(CreatorPayoutContext? context)
    {
        var contextProvider = new FakeCreatorPayoutContextProvider { ContextByPublicId = context };
        var ledgerRepository = new FakeLedgerEntryRepository();
        var payoutRepository = new FakePayoutRepository();
        var unitOfWork = new FakePayoutsUnitOfWork();
        var options = Options.Create(new PayoutsOptions { MinPayoutCents = 5000 });
        var payoutCreationService = new PayoutCreationService(ledgerRepository, payoutRepository, unitOfWork, options);

        var service = new PayoutAdminService(contextProvider, ledgerRepository, payoutRepository, unitOfWork, payoutCreationService, options);

        return (service, ledgerRepository, payoutRepository, unitOfWork);
    }

    private static CreatePayoutRequestDto BuildRequest(int amountCents = 10_000) => new()
    {
        CreatorPublicId = CreatorPublicId,
        AmountCents = amountCents,
        Currency = "EUR"
    };

    [Fact]
    public async Task CreatePayoutAsync_SufficientBalance_CreatesPayoutAndPayoutDebit()
    {
        var (service, ledgerRepository, payoutRepository, unitOfWork) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 20_000, Currency.Eur, Now));

        var result = await service.CreatePayoutAsync(BuildRequest(10_000), CancellationToken.None);

        Assert.Equal(PayoutStatus.Pending.ToString(), result.Status);
        Assert.Single(payoutRepository.Added);
        var debit = Assert.Single(ledgerRepository.Entries, e => e.Type == LedgerEntryType.PayoutDebit);
        Assert.Equal(-10_000, debit.AmountCents);
        Assert.Equal(CreatorId, unitOfWork.LastLockedCreatorId);
    }

    [Fact]
    public async Task CreatePayoutAsync_InsufficientBalance_ThrowsAndWritesNothing()
    {
        var (service, ledgerRepository, payoutRepository, _) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 5_000, Currency.Eur, Now));

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreatePayoutAsync(BuildRequest(10_000), CancellationToken.None));

        Assert.Empty(payoutRepository.Added);
        Assert.DoesNotContain(ledgerRepository.Entries, e => e.Type == LedgerEntryType.PayoutDebit);
    }

    [Fact]
    public async Task CreatePayoutAsync_BelowMinimum_Throws()
    {
        var (service, _, _, _) = BuildService(BuildContext());

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreatePayoutAsync(BuildRequest(1_000), CancellationToken.None));
    }

    [Fact]
    public async Task CreatePayoutAsync_StripeConnectCreator_Throws()
    {
        var (service, _, _, _) = BuildService(BuildContext(payoutMode: PayoutMode.StripeConnect));

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreatePayoutAsync(BuildRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task CreatePayoutAsync_NoPayoutProfile_Throws()
    {
        var (service, _, _, _) = BuildService(BuildContext(hasPayoutProfile: false));

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreatePayoutAsync(BuildRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task MarkFailedAsync_BooksAdjustmentThatRestoresBalance()
    {
        var (service, ledgerRepository, payoutRepository, _) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 20_000, Currency.Eur, Now));

        var created = await service.CreatePayoutAsync(BuildRequest(10_000), CancellationToken.None);
        payoutRepository.PayoutByPublicId = payoutRepository.Added[0];

        var balanceBeforeFailure = ledgerRepository.Entries
            .Where(e => e.CreatorId == CreatorId && e.Currency == Currency.Eur)
            .Sum(e => e.AmountCents);

        await service.MarkFailedAsync(created.PublicId, new MarkPayoutFailedRequestDto { Note = "bank rejected" }, CancellationToken.None);

        var balanceAfterFailure = ledgerRepository.Entries
            .Where(e => e.CreatorId == CreatorId && e.Currency == Currency.Eur)
            .Sum(e => e.AmountCents);

        Assert.Equal(20_000, balanceAfterFailure);
        Assert.Equal(10_000, balanceAfterFailure - balanceBeforeFailure);
        Assert.Contains(ledgerRepository.Entries, e => e.Type == LedgerEntryType.Adjustment && e.AmountCents == 10_000);
    }

    [Fact]
    public async Task MarkPaidAsync_AlreadyPaid_ThrowsConflict()
    {
        var (service, _, payoutRepository, _) = BuildService(BuildContext());
        var payout = Payout.Create(CreatorId, 10_000, Currency.Eur, null, Now);
        payout.MarkPaid("REF-1", Now);
        payoutRepository.PayoutByPublicId = payout;

        await Assert.ThrowsAsync<ConflictException>(
            () => service.MarkPaidAsync(payout.PublicId, new MarkPayoutPaidRequestDto { BankReference = "REF-2" }, CancellationToken.None));
    }
}
