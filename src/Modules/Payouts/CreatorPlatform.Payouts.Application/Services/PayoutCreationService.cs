using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Options;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Payouts.Application.Services;

public sealed class PayoutCreationService : IPayoutCreationService
{
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IPayoutRepository _payoutRepository;
    private readonly IPayoutsUnitOfWork _unitOfWork;
    private readonly PayoutsOptions _options;

    public PayoutCreationService(
        ILedgerEntryRepository ledgerEntryRepository,
        IPayoutRepository payoutRepository,
        IPayoutsUnitOfWork unitOfWork,
        IOptions<PayoutsOptions> options)
    {
        _ledgerEntryRepository = ledgerEntryRepository;
        _payoutRepository = payoutRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Payout> CreatePayoutAsync(
        CreatorPayoutContext creatorContext,
        int? amountCents,
        string? note,
        Func<Payout, Task>? onCreatedInTransaction,
        CancellationToken ct)
    {
        if (creatorContext.PayoutMode != PayoutMode.BankTransfer)
            throw new BadRequestException("Only BankTransfer creators can receive payouts.");

        if (!creatorContext.HasPayoutProfile)
            throw new BadRequestException("Creator has not set up a payout profile.");

        Payout? payout = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _unitOfWork.AcquireCreatorPayoutLockAsync(creatorContext.CreatorId, ct);

            if (await _payoutRepository.HasPendingPayoutAsync(creatorContext.CreatorId, ct))
                throw new ConflictException("You already have a pending payout request.");

            var balances = await _ledgerEntryRepository.GetBalanceByCreatorIdAsync(creatorContext.CreatorId, ct);
            var balanceCents = balances.FirstOrDefault(b => b.Currency == creatorContext.Currency)?.BalanceCents ?? 0;

            var effectiveAmountCents = amountCents ?? balanceCents;

            if (effectiveAmountCents < _options.MinPayoutCents)
                throw new BadRequestException($"Payout amount must be at least {_options.MinPayoutCents} cents.");

            if (balanceCents < effectiveAmountCents)
                throw new ConflictException("Creator's balance is insufficient for this payout.");

            var now = DateTimeOffset.UtcNow;
            payout = Payout.Create(creatorContext.CreatorId, effectiveAmountCents, creatorContext.Currency, note, now);
            await _payoutRepository.AddAsync(payout, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _ledgerEntryRepository.AddAsync(
                LedgerEntry.CreatePayoutDebit(creatorContext.CreatorId, payout.Id, -effectiveAmountCents, creatorContext.Currency, now), ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (onCreatedInTransaction is not null)
            {
                await onCreatedInTransaction(payout);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }, ct);

        return payout!;
    }
}
