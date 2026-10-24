using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Payouts.Application.Services;

public sealed class PayoutLedgerService : IPayoutLedgerService
{
    private readonly ILedgerEntryRepository _ledgerEntryRepository;
    private readonly ILogger<PayoutLedgerService> _logger;

    public PayoutLedgerService(ILedgerEntryRepository ledgerEntryRepository, ILogger<PayoutLedgerService> logger)
    {
        _ledgerEntryRepository = ledgerEntryRepository;
        _logger = logger;
    }

    public async Task AppendSaleAsync(
        int creatorId,
        int orderId,
        int amountCents,
        int platformFeeCents,
        Currency currency,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        if (await _ledgerEntryRepository.ExistsForOrderAsync(orderId, LedgerEntryType.SaleCredit, ct))
        {
            _logger.LogInformation("SaleCredit already booked for order {OrderId}, skipping (idempotent).", orderId);
            return;
        }

        await _ledgerEntryRepository.AddAsync(
            LedgerEntry.CreateSaleCredit(creatorId, orderId, amountCents, currency, occurredAt), ct);

        if (platformFeeCents > 0)
        {
            await _ledgerEntryRepository.AddAsync(
                LedgerEntry.CreateFeeDebit(creatorId, orderId, -platformFeeCents, currency, occurredAt), ct);
        }
    }

    public async Task AppendRefundAsync(
        int creatorId,
        int orderId,
        int amountCents,
        int platformFeeCents,
        int refundedToCents,
        Currency currency,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        if (refundedToCents <= 0 || refundedToCents > amountCents)
            throw new ArgumentOutOfRangeException(nameof(refundedToCents), refundedToCents, "The refunded amount must be within the sale.");

        var alreadyRefunded = -await _ledgerEntryRepository.SumForOrderAsync(orderId, LedgerEntryType.RefundDebit, ct);
        var refundDelta = refundedToCents - alreadyRefunded;
        if (refundDelta <= 0)
        {
            _logger.LogInformation(
                "Refund up to {RefundedToCents} already booked for order {OrderId}, skipping (idempotent).", refundedToCents, orderId);
            return;
        }

        await _ledgerEntryRepository.AddAsync(
            LedgerEntry.CreateRefundDebit(creatorId, orderId, -refundDelta, refundedToCents, currency, occurredAt), ct);

        var feeRefundedTo = ProportionalFee(platformFeeCents, refundedToCents, amountCents);
        var feeDelta = feeRefundedTo - await _ledgerEntryRepository.SumForOrderAsync(orderId, LedgerEntryType.FeeRefundCredit, ct);
        if (feeDelta > 0)
        {
            await _ledgerEntryRepository.AddAsync(
                LedgerEntry.CreateFeeRefundCredit(creatorId, orderId, feeDelta, refundedToCents, currency, occurredAt), ct);
        }
    }

    /// <summary>The fee's share for a refunded amount, half away from zero; the whole fee for a full refund.</summary>
    private static int ProportionalFee(int platformFeeCents, int refundedCents, int amountCents)
        => refundedCents >= amountCents
            ? platformFeeCents
            : (int)Math.Round((decimal)platformFeeCents * refundedCents / amountCents, MidpointRounding.AwayFromZero);
}
