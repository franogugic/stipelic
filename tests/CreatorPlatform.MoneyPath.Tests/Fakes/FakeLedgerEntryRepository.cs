using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Domain.Payouts;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeLedgerEntryRepository : ILedgerEntryRepository
{
    public List<LedgerEntry> Entries { get; } = [];

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
}
