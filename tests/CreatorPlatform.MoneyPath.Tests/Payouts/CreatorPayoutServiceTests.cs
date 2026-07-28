using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Options;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Payouts;

public class CreatorPayoutServiceTests
{
    private const string Slug = "acme";
    private const int OwnerUserId = 1;
    private const int CreatorId = 1;
    private static readonly Guid CreatorPublicId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreatorPayoutContext BuildContext(
        PayoutMode payoutMode = PayoutMode.BankTransfer,
        bool hasPayoutProfile = true,
        Currency currency = Currency.Eur) => new(
        CreatorId,
        CreatorPublicId,
        "Acme",
        Slug,
        CreatorStatus.Active,
        payoutMode,
        currency,
        hasPayoutProfile);

    private static (
        CreatorPayoutService Service,
        FakeLedgerEntryRepository LedgerRepository,
        FakePayoutRepository PayoutRepository,
        FakePayoutsUnitOfWork UnitOfWork,
        FakeEmailOutboxService EmailOutboxService)
        BuildService(CreatorPayoutContext? context, string? adminNotificationEmail = "admin@example.com")
    {
        var contextProvider = new FakeCreatorPayoutContextProvider { ContextBySlug = context };
        var ledgerRepository = new FakeLedgerEntryRepository();
        var payoutRepository = new FakePayoutRepository();
        var unitOfWork = new FakePayoutsUnitOfWork();
        var emailOutboxService = new FakeEmailOutboxService();
        var options = Options.Create(new PayoutsOptions { MinPayoutCents = 5000, AdminNotificationEmail = adminNotificationEmail });
        var payoutCreationService = new PayoutCreationService(ledgerRepository, payoutRepository, unitOfWork, options);

        var service = new CreatorPayoutService(
            contextProvider,
            ledgerRepository,
            payoutRepository,
            unitOfWork,
            payoutCreationService,
            emailOutboxService,
            options,
            NullLogger<CreatorPayoutService>.Instance);

        return (service, ledgerRepository, payoutRepository, unitOfWork, emailOutboxService);
    }

    [Fact]
    public async Task RequestPayoutAsync_SufficientBalance_CreatesPendingPayoutAndReducesBalance()
    {
        var (service, ledgerRepository, payoutRepository, _, _) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 20_000, Currency.Eur, Now));

        var result = await service.RequestPayoutAsync(Slug, OwnerUserId, 10_000, CancellationToken.None);

        Assert.Equal(nameof(PayoutStatus.Pending), result.Status);
        Assert.Equal(10_000, result.AmountCents);
        Assert.Single(payoutRepository.Added);

        var balance = ledgerRepository.Entries.Where(e => e.CreatorId == CreatorId).Sum(e => e.AmountCents);
        Assert.Equal(10_000, balance);
    }

    [Fact]
    public async Task RequestPayoutAsync_NullAmount_RequestsFullAvailableBalance()
    {
        var (service, ledgerRepository, payoutRepository, _, _) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 15_000, Currency.Eur, Now));

        var result = await service.RequestPayoutAsync(Slug, OwnerUserId, amountCents: null, CancellationToken.None);

        Assert.Equal(15_000, result.AmountCents);
        Assert.Single(payoutRepository.Added);
    }

    [Fact]
    public async Task RequestPayoutAsync_SecondRequestWhilePending_ThrowsConflictAndWritesNothing()
    {
        var (service, ledgerRepository, payoutRepository, _, _) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 40_000, Currency.Eur, Now));

        await service.RequestPayoutAsync(Slug, OwnerUserId, 10_000, CancellationToken.None);
        var countAfterFirst = payoutRepository.Added.Count;

        await Assert.ThrowsAsync<ConflictException>(
            () => service.RequestPayoutAsync(Slug, OwnerUserId, 10_000, CancellationToken.None));

        Assert.Equal(countAfterFirst, payoutRepository.Added.Count);
    }

    [Fact]
    public async Task RequestPayoutAsync_BelowMinimum_ThrowsBadRequest()
    {
        var (service, ledgerRepository, _, _, _) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 40_000, Currency.Eur, Now));

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.RequestPayoutAsync(Slug, OwnerUserId, 1_000, CancellationToken.None));
    }

    [Fact]
    public async Task RequestPayoutAsync_InsufficientBalance_ThrowsConflict()
    {
        var (service, ledgerRepository, _, _, _) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 5_000, Currency.Eur, Now));

        await Assert.ThrowsAsync<ConflictException>(
            () => service.RequestPayoutAsync(Slug, OwnerUserId, 10_000, CancellationToken.None));
    }

    [Fact]
    public async Task RequestPayoutAsync_StripeConnectCreator_ThrowsBadRequest()
    {
        var (service, ledgerRepository, _, _, _) = BuildService(BuildContext(payoutMode: PayoutMode.StripeConnect));
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 40_000, Currency.Eur, Now));

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.RequestPayoutAsync(Slug, OwnerUserId, 10_000, CancellationToken.None));
    }

    [Fact]
    public async Task RequestPayoutAsync_WrongSlug_ThrowsNotFound()
    {
        var (service, _, _, _, _) = BuildService(context: null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.RequestPayoutAsync("someone-elses-slug", OwnerUserId, 10_000, CancellationToken.None));
    }

    [Fact]
    public async Task RequestPayoutAsync_QueuesExactlyOnePayoutRequestedEmail()
    {
        var (service, ledgerRepository, _, _, emailOutboxService) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 40_000, Currency.Eur, Now));

        await service.RequestPayoutAsync(Slug, OwnerUserId, 10_000, CancellationToken.None);

        Assert.Equal(1, emailOutboxService.PayoutRequestedQueuedCount);
    }

    [Fact]
    public async Task RequestPayoutAsync_NoAdminEmailConfigured_SkipsWithoutException()
    {
        var (service, ledgerRepository, payoutRepository, _, emailOutboxService) =
            BuildService(BuildContext(), adminNotificationEmail: null);
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 40_000, Currency.Eur, Now));

        var exception = await Record.ExceptionAsync(
            () => service.RequestPayoutAsync(Slug, OwnerUserId, 10_000, CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(0, emailOutboxService.PayoutRequestedQueuedCount);
        Assert.Single(payoutRepository.Added);
    }

    [Fact]
    public async Task CancelPayoutRequestAsync_Pending_CancelsAndRestoresBalanceViaAdjustment()
    {
        var (service, ledgerRepository, payoutRepository, _, _) = BuildService(BuildContext());
        ledgerRepository.Entries.Add(LedgerEntry.CreateSaleCredit(CreatorId, 1, 20_000, Currency.Eur, Now));

        var requested = await service.RequestPayoutAsync(Slug, OwnerUserId, 10_000, CancellationToken.None);
        payoutRepository.PayoutByPublicId = payoutRepository.Added[0];

        var balanceAfterRequest = ledgerRepository.Entries.Where(e => e.CreatorId == CreatorId).Sum(e => e.AmountCents);
        Assert.Equal(10_000, balanceAfterRequest);

        var cancelled = await service.CancelPayoutRequestAsync(Slug, OwnerUserId, requested.PublicId, CancellationToken.None);

        Assert.Equal(nameof(PayoutStatus.Cancelled), cancelled.Status);
        var balanceAfterCancel = ledgerRepository.Entries.Where(e => e.CreatorId == CreatorId).Sum(e => e.AmountCents);
        Assert.Equal(20_000, balanceAfterCancel);
        Assert.Contains(ledgerRepository.Entries, e => e.Type == LedgerEntryType.Adjustment && e.AmountCents == 10_000);
    }

    [Fact]
    public async Task CancelPayoutRequestAsync_AlreadyPaid_ThrowsConflict()
    {
        var (service, _, payoutRepository, _, _) = BuildService(BuildContext());
        var payout = Payout.Create(CreatorId, 10_000, Currency.Eur, null, Now);
        payout.MarkPaid("REF-1", Now);
        payoutRepository.PayoutByPublicId = payout;

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CancelPayoutRequestAsync(Slug, OwnerUserId, payout.PublicId, CancellationToken.None));
    }
}
