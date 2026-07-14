using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Payouts.Infrastructure.Repositories;

public sealed class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly CreatorPlatformDbContext _context;

    public LedgerEntryRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LedgerEntry entry, CancellationToken ct)
    {
        await _context.Set<LedgerEntry>().AddAsync(entry, ct);
    }

    public async Task<bool> ExistsForOrderAsync(int orderId, LedgerEntryType type, CancellationToken ct)
    {
        return await _context.Set<LedgerEntry>()
            .AsNoTracking()
            .AnyAsync(e => e.OrderId == orderId && e.Type == type, ct);
    }

    public async Task<List<CreatorBalanceDto>> GetBalanceByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await _context.Set<LedgerEntry>()
            .AsNoTracking()
            .Where(e => e.CreatorId == creatorId)
            .GroupBy(e => e.Currency)
            .Select(g => new CreatorBalanceDto(g.Key, g.Sum(e => e.AmountCents)))
            .ToListAsync(ct);
    }
}
