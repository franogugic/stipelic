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
        Currency currency,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        if (await _ledgerEntryRepository.ExistsForOrderAsync(orderId, LedgerEntryType.RefundDebit, ct))
        {
            _logger.LogInformation("RefundDebit already booked for order {OrderId}, skipping (idempotent).", orderId);
            return;
        }

        await _ledgerEntryRepository.AddAsync(
            LedgerEntry.CreateRefundDebit(creatorId, orderId, -amountCents, currency, occurredAt), ct);

        if (platformFeeCents > 0)
        {
            await _ledgerEntryRepository.AddAsync(
                LedgerEntry.CreateFeeRefundCredit(creatorId, orderId, platformFeeCents, currency, occurredAt), ct);
        }
    }
}
