using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Options;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Payouts.Application.Services;

public sealed class PayoutAdminService : IPayoutAdminService
{
    private const int DefaultBalancesLimit = 100;

    private readonly ICreatorPayoutContextProvider _creatorPayoutContextProvider;
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IPayoutRepository _payoutRepository;
    private readonly IPayoutsUnitOfWork _unitOfWork;
    private readonly PayoutsOptions _options;

    public PayoutAdminService(
        ICreatorPayoutContextProvider creatorPayoutContextProvider,
        ILedgerEntryRepository ledgerEntryRepository,
        IPayoutRepository payoutRepository,
        IPayoutsUnitOfWork unitOfWork,
        IOptions<PayoutsOptions> options)
    {
        _creatorPayoutContextProvider = creatorPayoutContextProvider;
        _ledgerEntryRepository = ledgerEntryRepository;
        _payoutRepository = payoutRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<List<CreatorBalanceSummaryDto>> GetBalancesAsync(int? minCents, int limit, CancellationToken ct)
    {
        var effectiveMinCents = minCents ?? _options.MinPayoutCents;
        var effectiveLimit = limit <= 0 ? DefaultBalancesLimit : limit;

        return await _ledgerEntryRepository.GetBalancesForPayoutAsync(effectiveMinCents, effectiveLimit, ct);
    }

    public async Task<PayoutDto> CreatePayoutAsync(CreatePayoutRequestDto request, CancellationToken ct)
    {
        var creatorContext = await _creatorPayoutContextProvider.GetByPublicIdAsync(request.CreatorPublicId, ct);
        if (creatorContext is null)
            throw new NotFoundException("Creator not found.");

        if (creatorContext.PayoutMode != PayoutMode.BankTransfer)
            throw new BadRequestException("Only BankTransfer creators can receive payouts.");

        if (!creatorContext.HasPayoutProfile)
            throw new BadRequestException("Creator has not set up a payout profile.");

        var amountCents = request.AmountCents;
        if (amountCents < _options.MinPayoutCents)
            throw new BadRequestException($"Payout amount must be at least {_options.MinPayoutCents} cents.");

        var currency = ParseCurrency(request.Currency);
        if (currency != creatorContext.Currency)
            throw new BadRequestException("Currency does not match the creator's default currency.");

        var note = request.Note;
        Payout? payout = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _unitOfWork.AcquireCreatorPayoutLockAsync(creatorContext.CreatorId, ct);

            var balances = await _ledgerEntryRepository.GetBalanceByCreatorIdAsync(creatorContext.CreatorId, ct);
            var balanceCents = balances.FirstOrDefault(b => b.Currency == currency)?.BalanceCents ?? 0;

            if (balanceCents < amountCents)
                throw new ConflictException("Creator's balance is insufficient for this payout.");

            var now = DateTimeOffset.UtcNow;
            payout = Payout.Create(creatorContext.CreatorId, amountCents, currency, note, now);
            await _payoutRepository.AddAsync(payout, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            await _ledgerEntryRepository.AddAsync(
                LedgerEntry.CreatePayoutDebit(creatorContext.CreatorId, payout.Id, -amountCents, currency, now), ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return ToDto(payout!);
    }

    public async Task<PayoutDto> MarkPaidAsync(Guid payoutPublicId, MarkPayoutPaidRequestDto request, CancellationToken ct)
    {
        var payout = await _payoutRepository.GetByPublicIdForUpdateAsync(payoutPublicId, ct);
        if (payout is null)
            throw new NotFoundException("Payout not found.");

        if (string.IsNullOrWhiteSpace(request.BankReference))
            throw new BadRequestException("Bank reference is required.");

        try
        {
            payout.MarkPaid(request.BankReference.Trim(), DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException e)
        {
            throw new ConflictException(e.Message);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(payout);
    }

    public async Task<PayoutDto> MarkFailedAsync(Guid payoutPublicId, MarkPayoutFailedRequestDto request, CancellationToken ct)
    {
        PayoutDto? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var payout = await _payoutRepository.GetByPublicIdForUpdateAsync(payoutPublicId, ct);
            if (payout is null)
                throw new NotFoundException("Payout not found.");

            var now = DateTimeOffset.UtcNow;

            try
            {
                payout.MarkFailed(request.Note, now);
            }
            catch (InvalidOperationException e)
            {
                throw new ConflictException(e.Message);
            }

            await _ledgerEntryRepository.AddAsync(
                LedgerEntry.CreateAdjustment(payout.CreatorId, payout.Id, payout.AmountCents, payout.Currency, now), ct);

            await _unitOfWork.SaveChangesAsync(ct);

            result = ToDto(payout);
        }, ct);

        return result!;
    }

    private static Currency ParseCurrency(string currency)
    {
        return currency.Trim().ToUpperInvariant() switch
        {
            "EUR" => Currency.Eur,
            "USD" => Currency.Usd,
            _ => throw new BadRequestException("Currency is not supported.")
        };
    }

    private static PayoutDto ToDto(Payout payout) => new(
        payout.PublicId,
        payout.AmountCents,
        payout.Currency.ToString(),
        payout.Status.ToString(),
        payout.BankReference,
        payout.Note,
        payout.CreatedAt,
        payout.PaidAt);
}
