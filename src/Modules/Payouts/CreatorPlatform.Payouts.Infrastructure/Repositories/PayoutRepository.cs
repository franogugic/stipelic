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
}
