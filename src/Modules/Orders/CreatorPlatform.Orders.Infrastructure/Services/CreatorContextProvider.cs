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
        var result = await (
            from lp in _context.Set<LandingPage>().AsNoTracking()
            join c in _context.Set<Creator>().AsNoTracking() on lp.CreatorId equals c.Id
            join p in _context.Set<Product>().AsNoTracking() on lp.ProductId equals p.Id
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
                p.PriceCents,
                Currency = c.DefaultCurrency
            }
        ).FirstOrDefaultAsync(ct);

        if (result is null)
            return null;

        return new LandingPageProductInfo(
            result.Id,
            result.ProductId,
            result.LandingPageId,
            result.ProductName,
            result.PriceCents,
            result.Currency);
    }

    public async Task<string?> GetProductNameAsync(int productId, CancellationToken ct)
    {
        return await _context.Set<Product>()
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);
    }
}
