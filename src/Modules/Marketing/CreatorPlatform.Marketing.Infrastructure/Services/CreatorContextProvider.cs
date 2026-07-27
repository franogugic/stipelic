using CreatorPlatform.Auth.Domain.Users;
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
        return await ProjectContext(_context.Set<Creator>()
                .AsNoTracking()
                .Where(c => c.Slug == slug && c.OwnerUserId == ownerUserId && c.Status != CreatorStatus.Disabled))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<MarketingCreatorContext?> GetByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await ProjectContext(_context.Set<Creator>()
                .AsNoTracking()
                .Where(c => c.Id == creatorId && c.Status != CreatorStatus.Disabled))
            .FirstOrDefaultAsync(ct);
    }

    private IQueryable<MarketingCreatorContext> ProjectContext(IQueryable<Creator> creators)
    {
        return creators.Select(c => new MarketingCreatorContext(
            c.Id,
            c.PublicId,
            c.Name,
            c.Slug,
            _context.Set<CreatorSettings>()
                .Where(s => s.CreatorId == c.Id)
                .Select(s => s.SupportEmail)
                .FirstOrDefault(),
            _context.Set<CreatorSettings>()
                .Where(s => s.CreatorId == c.Id)
                .Select(s => s.BrandName)
                .FirstOrDefault() ?? c.Name,
            _context.Set<CreatorSettings>()
                .Where(s => s.CreatorId == c.Id)
                .Select(s => s.LogoUrl)
                .FirstOrDefault(),
            _context.Set<CreatorSettings>()
                .Where(s => s.CreatorId == c.Id)
                .Select(s => s.PrimaryColor)
                .FirstOrDefault() ?? "#111111",
            _context.Set<User>()
                .Where(u => u.Id == c.OwnerUserId)
                .Select(u => u.Email)
                .First()));
    }

    public async Task<Dictionary<int, Guid>> GetLandingPagePublicIdsAsync(IReadOnlyCollection<int> landingPageIds, CancellationToken ct)
    {
        if (landingPageIds.Count == 0)
            return new Dictionary<int, Guid>();

        return await _context.Set<LandingPage>()
            .AsNoTracking()
            .Where(lp => landingPageIds.Contains(lp.Id))
            .Select(lp => new { lp.Id, lp.PublicId })
            .ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);
    }

    public async Task<Dictionary<int, Guid>> GetProductPublicIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        if (productIds.Count == 0)
            return new Dictionary<int, Guid>();

        return await _context.Set<Product>()
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId })
            .ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);
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
