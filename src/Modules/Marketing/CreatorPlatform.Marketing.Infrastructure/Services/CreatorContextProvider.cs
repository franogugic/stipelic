using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<MarketingCreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.Slug == slug && c.OwnerUserId == ownerUserId && c.Status != CreatorStatus.Disabled)
            .Select(c => new MarketingCreatorContext(
                c.Id,
                c.PublicId,
                c.Name,
                c.Slug,
                _context.Set<CreatorSettings>()
                    .Where(s => s.CreatorId == c.Id)
                    .Select(s => s.SupportEmail)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int?> ResolveLandingPageIdAsync(int creatorId, Guid landingPagePublicId, CancellationToken ct)
    {
        return await _context.Set<LandingPage>()
            .AsNoTracking()
            .Where(lp => lp.PublicId == landingPagePublicId && lp.CreatorId == creatorId)
            .Select(lp => (int?)lp.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int?> ResolveProductIdAsync(int creatorId, Guid productPublicId, CancellationToken ct)
    {
        return await _context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.PublicId == productPublicId && p.CreatorId == creatorId)
            .Select(p => (int?)p.Id)
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
