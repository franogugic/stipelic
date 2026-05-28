using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Creators.Infrastructure.Repositories;

public sealed class CreatorSubscriptionRepository : ICreatorSubscriptionRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorSubscriptionRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CreatorSubscription subscription, CancellationToken ct)
    {
        await _context.Set<CreatorSubscription>().AddAsync(subscription, ct);
    }

    public async Task<CreatorSubscription?> GetCurrentByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await _context
            .Set<CreatorSubscription>()
            .AsNoTracking()
            .Include(subscription => subscription.Plan)
            .Where(subscription =>
                subscription.CreatorId == creatorId
                && subscription.Status != CreatorSubscriptionStatus.Cancelled)
            .OrderByDescending(subscription => subscription.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
