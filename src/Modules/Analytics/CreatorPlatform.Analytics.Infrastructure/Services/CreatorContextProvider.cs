using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Analytics.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<int?> GetCreatorIdBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.Slug == slug && c.OwnerUserId == ownerUserId && c.Status != CreatorStatus.Disabled)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int?> GetActivePlanLimitAsync(int creatorId, string limitKey, CancellationToken ct)
    {
        return await (
            from cs in _context.Set<CreatorSubscription>().AsNoTracking()
            join limit in _context.Set<CreatorPlanLimit>().AsNoTracking()
                on cs.PlanId equals limit.PlanId
            where cs.CreatorId == creatorId
                && cs.Status == CreatorSubscriptionStatus.Active
                && limit.LimitKey == limitKey
            select (int?)limit.LimitValue
        ).FirstOrDefaultAsync(ct);
    }
}
