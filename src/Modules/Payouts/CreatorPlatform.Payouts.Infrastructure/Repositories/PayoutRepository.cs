using CreatorPlatform.Creators.Domain.Creators;
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

    public async Task<List<AdminPayoutQueueItemDto>> ListForQueueAsync(PayoutStatus? status, int limit, CancellationToken ct)
    {
        var query = _context.Set<Payout>().AsNoTracking().AsQueryable();
        if (status is not null)
            query = query.Where(p => p.Status == status.Value);

        var ordered = status == PayoutStatus.Pending
            ? query.OrderBy(p => p.CreatedAt)
            : query.OrderByDescending(p => p.CreatedAt);

        // Enum -> string conversion is not reliably translatable in a SQL projection (same reasoning as
        // GetBalancesForPayoutAsync): materialize the joined rows first, then map to the DTO in-memory.
        var rows = await ordered
            .Take(limit)
            .Join(_context.Set<Creator>(), p => p.CreatorId, c => c.Id, (p, c) => new { Payout = p, Creator = c })
            .GroupJoin(
                _context.Set<CreatorPayoutProfile>(),
                pc => pc.Creator.Id,
                profile => profile.CreatorId,
                (pc, profiles) => new { pc.Payout, pc.Creator, Profiles = profiles })
            .SelectMany(
                x => x.Profiles.DefaultIfEmpty(),
                (x, profile) => new
                {
                    x.Payout.PublicId,
                    CreatorPublicId = x.Creator.PublicId,
                    CreatorName = x.Creator.Name,
                    CreatorSlug = x.Creator.Slug,
                    x.Payout.AmountCents,
                    x.Payout.Currency,
                    x.Payout.Status,
                    x.Payout.BankReference,
                    x.Payout.Note,
                    x.Payout.CreatedAt,
                    x.Payout.PaidAt,
                    AccountHolderName = profile != null ? profile.AccountHolderName : null,
                    Iban = profile != null ? profile.Iban : null,
                    BankCountryCode = profile != null ? profile.BankCountryCode : null,
                })
            .ToListAsync(ct);

        return rows.Select(r => new AdminPayoutQueueItemDto(
            r.PublicId,
            r.CreatorPublicId,
            r.CreatorName,
            r.CreatorSlug,
            r.AmountCents,
            r.Currency.ToString(),
            r.Status.ToString(),
            r.BankReference,
            r.Note,
            r.CreatedAt,
            r.PaidAt,
            r.AccountHolderName ?? string.Empty,
            r.Iban ?? string.Empty,
            r.BankCountryCode ?? string.Empty)).ToList();
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
