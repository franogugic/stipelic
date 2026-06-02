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

    public async Task<CreatorSubscription?> GetByIdForUpdateAsync(int id, CancellationToken ct)
    {
        return await _context
            .Set<CreatorSubscription>()
            .Include(subscription => subscription.Plan)
            .FirstOrDefaultAsync(subscription => subscription.Id == id, ct);
    }

    public async Task<CreatorSubscription?> GetByProviderSubscriptionIdForUpdateAsync(
        string providerSubscriptionId,
        CancellationToken ct)
    {
        return await _context
            .Set<CreatorSubscription>()
            .Include(subscription => subscription.Creator)
            .Include(subscription => subscription.Plan)
            .Where(subscription =>
                subscription.ProviderSubscriptionId == providerSubscriptionId
                && subscription.Status != CreatorSubscriptionStatus.Cancelled)
            .OrderByDescending(subscription => subscription.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
