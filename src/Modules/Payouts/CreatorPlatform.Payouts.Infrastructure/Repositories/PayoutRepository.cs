using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Domain.Payouts;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Payouts.Infrastructure.Repositories;

public sealed class PayoutRepository : IPayoutRepository
{
    private readonly CreatorPlatformDbContext _context;

    public PayoutRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Payout payout, CancellationToken ct)
    {
        await _context.Set<Payout>().AddAsync(payout, ct);
    }

    public async Task<Payout?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct)
    {
        return await _context.Set<Payout>()
            .FirstOrDefaultAsync(p => p.PublicId == publicId, ct);
    }

    public async Task<int> GetPendingAmountCentsByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await _context.Set<Payout>()
            .AsNoTracking()
            .Where(p => p.CreatorId == creatorId && p.Status == PayoutStatus.Pending)
            .SumAsync(p => p.AmountCents, ct);
    }

    public async Task<bool> HasPendingPayoutAsync(int creatorId, CancellationToken ct)
    {
        return await _context.Set<Payout>()
            .AsNoTracking()
            .AnyAsync(p => p.CreatorId == creatorId && p.Status == PayoutStatus.Pending, ct);
    }

    public async Task<List<PayoutDto>> ListRecentByCreatorIdAsync(int creatorId, int limit, CancellationToken ct)
    {
        return await _context.Set<Payout>()
            .AsNoTracking()
            .Where(p => p.CreatorId == creatorId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .Select(p => new PayoutDto(
                p.PublicId,
                p.AmountCents,
                p.Currency.ToString(),
                p.Status.ToString(),
                p.BankReference,
                p.Note,
                p.CreatedAt,
                p.PaidAt))
            .ToListAsync(ct);
    }
}
