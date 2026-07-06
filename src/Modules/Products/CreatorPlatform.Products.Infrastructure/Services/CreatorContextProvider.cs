using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private const string MaxProductsLimitKey = "max_products";
    private const int DefaultMaxProducts = 1;

    private readonly CreatorPlatformDbContext _context;

    public CreatorContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<CreatorContext?> GetBySlugForOwnerAsync(
        string slug,
        int ownerUserId,
        CancellationToken ct)
    {
        var creator = await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.Slug == slug && c.OwnerUserId == ownerUserId && c.Status != CreatorStatus.Disabled)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);

        if (creator is null)
            return null;

        var maxProducts = await _context.Set<CreatorSubscription>()
            .AsNoTracking()
            .Where(cs => cs.CreatorId == creator.Id && cs.Status != CreatorSubscriptionStatus.Cancelled)
            .OrderByDescending(cs => cs.CreatedAt)
            .SelectMany(cs => _context.Set<CreatorPlanLimit>()
                .Where(cpl => cpl.PlanId == cs.PlanId && cpl.LimitKey == MaxProductsLimitKey)
                .Select(cpl => (int?)cpl.LimitValue))
            .FirstOrDefaultAsync(ct) ?? DefaultMaxProducts;

        var activeProductCount = await _context.Set<Product>()
            .AsNoTracking()
            .CountAsync(p => p.CreatorId == creator.Id && p.Status != ProductStatus.Archived, ct);

        return new CreatorContext(creator.Id, maxProducts, activeProductCount);
    }
}
