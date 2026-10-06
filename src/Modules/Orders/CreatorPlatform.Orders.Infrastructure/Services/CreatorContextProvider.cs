using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Orders.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<LandingPageProductInfo?> GetProductInfoByLandingPageSlugAsync(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct)
    {
        // Active-subscription fee, pre-projected to a nullable int so the later LEFT JOIN naturally
        // yields null (not a default 0) when a creator has no active subscription.
        var activeSubscriptionFees =
            from cs in _context.Set<CreatorSubscription>().AsNoTracking()
            join plan in _context.Set<CreatorPlan>().AsNoTracking() on cs.PlanId equals plan.Id
            where cs.Status == CreatorSubscriptionStatus.Active
            select new { cs.CreatorId, PlatformFeeBasisPoints = (int?)plan.PlatformFeeBasisPoints };

        var result = await (
            from lp in _context.Set<LandingPage>().AsNoTracking()
            join c in _context.Set<Creator>().AsNoTracking() on lp.CreatorId equals c.Id
            join p in _context.Set<Product>().AsNoTracking() on lp.ProductId equals p.Id
            join fee in activeSubscriptionFees on c.Id equals fee.CreatorId into fees
            from fee in fees.DefaultIfEmpty()
            where c.Slug == creatorSlug
                && lp.Slug == landingPageSlug
                && lp.Status == LandingPageStatus.Published
                && c.Status != CreatorStatus.Disabled
            select new
            {
                c.Id,
                ProductId = p.Id,
                LandingPageId = lp.Id,
                ProductName = p.Name,
                p.ThumbnailUrl,
                p.PriceCents,
                Currency = c.DefaultCurrency,
                c.Status,
                c.PayoutMode,
                c.StripeConnectAccountId,
                c.StripeConnectPayoutsEnabled,
                HasPayoutProfile = _context.Set<CreatorPayoutProfile>().Any(pp => pp.CreatorId == c.Id),
                fee.PlatformFeeBasisPoints
            }
        ).FirstOrDefaultAsync(ct);

        if (result is null)
            return null;

        return new LandingPageProductInfo(
            result.Id,
            result.ProductId,
            result.LandingPageId,
            result.ProductName,
            result.ThumbnailUrl,
            result.PriceCents,
            result.Currency,
            result.Status,
            result.PayoutMode,
            result.StripeConnectAccountId,
            result.StripeConnectPayoutsEnabled,
            result.HasPayoutProfile,
            result.PlatformFeeBasisPoints);
    }

    public async Task<OrderEmailContext?> GetOrderEmailContextAsync(int productId, CancellationToken ct)
    {
        var row = await (
            from p in _context.Set<Product>().AsNoTracking()
            join c in _context.Set<Creator>().AsNoTracking() on p.CreatorId equals c.Id
            from s in _context.Set<CreatorSettings>().AsNoTracking().Where(s => s.CreatorId == c.Id).DefaultIfEmpty()
            where p.Id == productId
            select new
            {
                p.Name,
                p.Type,
                p.ThumbnailUrl,
                CreatorName = c.Name,
                BrandName = s != null ? s.BrandName : null,
                PrimaryColor = s != null ? s.PrimaryColor : null,
                LogoUrl = s != null ? s.LogoUrl : null,
                SupportEmail = s != null ? s.SupportEmail : null,
            }).FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        return new OrderEmailContext(
            row.Name,
            row.Type switch
            {
                ProductType.Digital => "Digital download",
                ProductType.Course => "Online course",
                _ => "Service",
            },
            row.ThumbnailUrl,
            string.IsNullOrWhiteSpace(row.BrandName) ? row.CreatorName : row.BrandName,
            row.PrimaryColor,
            row.LogoUrl,
            row.SupportEmail);
    }
}
