using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Payouts.Application.Services;

public sealed class CreatorPayoutService : ICreatorPayoutService
{
    private const int HistoryLimit = 50;

    private readonly ICreatorPayoutContextProvider _creatorPayoutContextProvider;
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IPayoutRepository _payoutRepository;
    private readonly PayoutsOptions _options;

    public CreatorPayoutService(
        ICreatorPayoutContextProvider creatorPayoutContextProvider,
        ILedgerEntryRepository ledgerEntryRepository,
        IPayoutRepository payoutRepository,
        IOptions<PayoutsOptions> options)
    {
        _creatorPayoutContextProvider = creatorPayoutContextProvider;
        _ledgerEntryRepository = ledgerEntryRepository;
        _payoutRepository = payoutRepository;
        _options = options.Value;
    }

    public async Task<CreatorPayoutSummaryDto?> GetSummaryAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await _creatorPayoutContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        if (context.PayoutMode != PayoutMode.BankTransfer)
            return null;

        var balances = await _ledgerEntryRepository.GetBalanceByCreatorIdAsync(context.CreatorId, ct);
        var balanceCents = balances.FirstOrDefault(b => b.Currency == context.Currency)?.BalanceCents ?? 0;
        var pendingCents = await _payoutRepository.GetPendingAmountCentsByCreatorIdAsync(context.CreatorId, ct);

        return new CreatorPayoutSummaryDto(context.Currency.ToString(), balanceCents, pendingCents, _options.MinPayoutCents);
    }

    public async Task<List<PayoutDto>> GetHistoryAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await _creatorPayoutContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        return await _payoutRepository.ListRecentByCreatorIdAsync(context.CreatorId, HistoryLimit, ct);
    }
}
