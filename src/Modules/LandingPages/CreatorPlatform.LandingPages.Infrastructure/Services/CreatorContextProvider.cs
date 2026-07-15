using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.LandingPages.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private const string MaxLandingPagesLimitKey = "max_landing_pages";
    private const int DefaultMaxLandingPages = 1;

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
            .Select(c => new
            {
                c.Id,
                c.PayoutMode,
                c.StripeConnectPayoutsEnabled,
                HasPayoutProfile = _context.Set<CreatorPayoutProfile>().Any(pp => pp.CreatorId == c.Id)
            })
            .FirstOrDefaultAsync(ct);

        if (creator is null)
            return null;

        var maxLandingPages = await _context.Set<CreatorSubscription>()
            .AsNoTracking()
            .Where(cs => cs.CreatorId == creator.Id && cs.Status != CreatorSubscriptionStatus.Cancelled)
            .OrderByDescending(cs => cs.CreatedAt)
            .SelectMany(cs => _context.Set<CreatorPlanLimit>()
                .Where(cpl => cpl.PlanId == cs.PlanId && cpl.LimitKey == MaxLandingPagesLimitKey)
                .Select(cpl => (int?)cpl.LimitValue))
            .FirstOrDefaultAsync(ct) ?? DefaultMaxLandingPages;

        var activeLandingPageCount = await _context.Set<LandingPage>()
            .AsNoTracking()
            .CountAsync(lp => lp.CreatorId == creator.Id && lp.Status != LandingPageStatus.Archived, ct);

        return new CreatorContext(creator.Id, maxLandingPages, activeLandingPageCount)
        {
            PayoutMode = creator.PayoutMode,
            StripeConnectPayoutsEnabled = creator.StripeConnectPayoutsEnabled,
            HasPayoutProfile = creator.HasPayoutProfile
        };
    }

    public async Task<int?> GetProductIdForCreatorAsync(int creatorId, Guid productPublicId, CancellationToken ct)
    {
        return await _context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.PublicId == productPublicId && p.CreatorId == creatorId)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ProductInfo?> GetProductInfoAsync(int productId, CancellationToken ct)
    {
        return await _context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new ProductInfo(p.Name, p.PriceCents))
            .FirstOrDefaultAsync(ct);
    }
}
