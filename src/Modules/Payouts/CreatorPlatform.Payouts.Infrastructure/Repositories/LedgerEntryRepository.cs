using CreatorPlatform.Creators.Domain.Creators;
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

    public async Task<List<CreatorBalanceSummaryDto>> GetBalancesForPayoutAsync(int minCents, int limit, CancellationToken ct)
    {
        var query =
            from le in _context.Set<LedgerEntry>().AsNoTracking()
            join c in _context.Set<Creator>().AsNoTracking() on le.CreatorId equals c.Id
            where c.PayoutMode == PayoutMode.BankTransfer && c.Status != CreatorStatus.Disabled
            group le by new { c.Id, c.PublicId, c.Name, c.Slug, le.Currency } into g
            select new
            {
                g.Key.Id,
                g.Key.PublicId,
                g.Key.Name,
                g.Key.Slug,
                g.Key.Currency,
                BalanceCents = g.Sum(e => e.AmountCents)
            };

        var rows = await query
            .Where(x => x.BalanceCents >= minCents)
            .OrderByDescending(x => x.BalanceCents)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.PublicId,
                x.Name,
                x.Slug,
                x.Currency,
                x.BalanceCents,
                HasPayoutProfile = _context.Set<CreatorPayoutProfile>().Any(p => p.CreatorId == x.Id)
            })
            .ToListAsync(ct);

        // Currency.ToString() is not reliably translatable to SQL, so it's applied in-memory after
        // materializing the (limit-bounded) result set, not inside the query above.
        return rows
            .Select(r => new CreatorBalanceSummaryDto(
                r.PublicId,
                r.Name,
                r.Slug,
                r.Currency.ToString(),
                r.BalanceCents,
                r.HasPayoutProfile))
            .ToList();
    }
}
