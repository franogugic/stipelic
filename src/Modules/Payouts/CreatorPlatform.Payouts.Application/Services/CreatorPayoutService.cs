using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Options;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Payouts.Application.Services;

public sealed class CreatorPayoutService : ICreatorPayoutService
{
    private const int HistoryLimit = 50;

    private readonly ICreatorPayoutContextProvider _creatorPayoutContextProvider;
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly IPayoutRepository _payoutRepository;
    private readonly IPayoutsUnitOfWork _unitOfWork;
    private readonly IPayoutCreationService _payoutCreationService;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly PayoutsOptions _options;
    private readonly ILogger<CreatorPayoutService> _logger;

    public CreatorPayoutService(
        ICreatorPayoutContextProvider creatorPayoutContextProvider,
        ILedgerEntryRepository ledgerEntryRepository,
        IPayoutRepository payoutRepository,
        IPayoutsUnitOfWork unitOfWork,
        IPayoutCreationService payoutCreationService,
        IEmailOutboxService emailOutboxService,
        IOptions<PayoutsOptions> options,
        ILogger<CreatorPayoutService> logger)
    {
        _creatorPayoutContextProvider = creatorPayoutContextProvider;
        _ledgerEntryRepository = ledgerEntryRepository;
        _payoutRepository = payoutRepository;
        _unitOfWork = unitOfWork;
        _payoutCreationService = payoutCreationService;
        _emailOutboxService = emailOutboxService;
        _options = options.Value;
        _logger = logger;
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

    public async Task<PayoutDto> RequestPayoutAsync(string slug, int ownerUserId, int? amountCents, CancellationToken ct)
    {
        var context = await _creatorPayoutContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        var payout = await _payoutCreationService.CreatePayoutAsync(
            context,
            amountCents,
            note: null,
            onCreatedInTransaction: createdPayout => QueueAdminNotificationAsync(context, createdPayout, ct),
            ct);

        return ToDto(payout);
    }

    public async Task<PayoutDto> CancelPayoutRequestAsync(string slug, int ownerUserId, Guid payoutPublicId, CancellationToken ct)
    {
        var context = await _creatorPayoutContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        PayoutDto? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var payout = await _payoutRepository.GetByPublicIdForUpdateAsync(payoutPublicId, ct);
            if (payout is null || payout.CreatorId != context.CreatorId)
                throw new NotFoundException("Payout not found.");

            var now = DateTimeOffset.UtcNow;

            try
            {
                payout.Cancel(now);
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

    private async Task QueueAdminNotificationAsync(CreatorPayoutContext context, Payout payout, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.AdminNotificationEmail))
        {
            _logger.LogWarning(
                "Payout requested by creator {CreatorId} but Payouts:AdminNotificationEmail is not configured — skipping admin notification email.",
                context.CreatorId);
            return;
        }

        await _emailOutboxService.QueuePayoutRequestedAsync(
            _options.AdminNotificationEmail,
            payout.PublicId.ToString(),
            context.Name,
            context.Slug,
            payout.AmountCents,
            context.Currency.ToString(),
            ct);
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
