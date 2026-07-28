using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Domain.Payouts;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeLedgerEntryRepository : ILedgerEntryRepository
{
    public List<LedgerEntry> Entries { get; } = [];

    /// <summary>Backing data for <see cref="GetBalancesForPayoutAsync"/> — set directly by tests instead
    /// of being derived from <see cref="Entries"/>, since the real query also joins creator/profile data
    /// this in-memory fake doesn't otherwise have.</summary>
    public List<CreatorBalanceSummaryDto> BalancesForPayout { get; set; } = [];

    public Task AddAsync(LedgerEntry entry, CancellationToken ct)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsForOrderAsync(int orderId, LedgerEntryType type, CancellationToken ct)
        => Task.FromResult(Entries.Any(e => e.OrderId == orderId && e.Type == type));

    public Task<List<CreatorBalanceDto>> GetBalanceByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(Entries
            .Where(e => e.CreatorId == creatorId)
            .GroupBy(e => e.Currency)
            .Select(g => new CreatorBalanceDto(g.Key, g.Sum(e => e.AmountCents)))
            .ToList());

    public Task<List<CreatorBalanceSummaryDto>> GetBalancesForPayoutAsync(int minCents, int limit, CancellationToken ct)
        => Task.FromResult(BalancesForPayout
            .Where(b => b.BalanceCents >= minCents)
            .OrderByDescending(b => b.BalanceCents)
            .Take(limit)
            .ToList());
}
