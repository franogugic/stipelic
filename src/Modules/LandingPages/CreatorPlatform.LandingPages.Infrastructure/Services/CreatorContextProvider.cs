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
                c.Status,
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
            // The plan in force: a pending upgrade from Free must not lift the limits before it is paid.
            .OrderBy(cs => cs.Status == CreatorSubscriptionStatus.PendingPayment ? 1 : 0)
            .ThenByDescending(cs => cs.CreatedAt)
            .SelectMany(cs => _context.Set<CreatorPlanLimit>()
                .Where(cpl => cpl.PlanId == cs.PlanId && cpl.LimitKey == MaxLandingPagesLimitKey)
                .Select(cpl => (int?)cpl.LimitValue))
            .FirstOrDefaultAsync(ct) ?? DefaultMaxLandingPages;

        var activeLandingPageCount = await _context.Set<LandingPage>()
            .AsNoTracking()
            .CountAsync(lp => lp.CreatorId == creator.Id && lp.Status != LandingPageStatus.Archived, ct);

        return new CreatorContext(creator.Id, maxLandingPages, activeLandingPageCount)
        {
            Status = creator.Status,
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
            .Select(p => new ProductInfo(p.PublicId, p.Name, p.PriceCents, p.ThumbnailUrl, p.Type.ToString()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Dictionary<int, ProductInfo>> GetProductInfosAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        if (productIds.Count == 0)
            return [];

        return await _context.Set<Product>()
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ProductInfo(p.PublicId, p.Name, p.PriceCents, p.ThumbnailUrl, p.Type.ToString()), ct);
    }

    public Task<PublicCreatorBrand?> GetPublicBrandAsync(int creatorId, CancellationToken ct) =>
        GetPublicBrandAsync(c => c.Id == creatorId, ct);

    public Task<PublicCreatorBrand?> GetPublicBrandBySlugAsync(string creatorSlug, CancellationToken ct) =>
        GetPublicBrandAsync(c => c.Slug == creatorSlug && c.Status != CreatorStatus.Disabled, ct);

    private async Task<PublicCreatorBrand?> GetPublicBrandAsync(
        System.Linq.Expressions.Expression<Func<Creator, bool>> creatorFilter, CancellationToken ct)
    {
        var row = await (
            from c in _context.Set<Creator>().AsNoTracking().Where(creatorFilter)
            from s in _context.Set<CreatorSettings>().AsNoTracking().Where(s => s.CreatorId == c.Id).DefaultIfEmpty()
            select new
            {
                c.Id,
                c.Name,
                c.DefaultCurrency,
                BrandName = s != null ? s.BrandName : null,
                PrimaryColor = s != null ? s.PrimaryColor : null,
                LogoUrl = s != null ? s.LogoUrl : null,
            }).FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new PublicCreatorBrand(
                row.Id,
                string.IsNullOrWhiteSpace(row.BrandName) ? row.Name : row.BrandName,
                row.PrimaryColor,
                row.LogoUrl,
                row.DefaultCurrency.ToString());
    }
}
