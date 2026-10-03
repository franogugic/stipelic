using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Services;

public sealed class LandingPageContextProvider : ILandingPageContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public LandingPageContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsUsedByPublishedPageAsync(int productId, CancellationToken ct)
    {
        return await _context.Set<LandingPage>()
            .AsNoTracking()
            .AnyAsync(lp => lp.ProductId == productId && lp.Status == LandingPageStatus.Published, ct);
    }

    public async Task<List<ProductSellingPageDto>> GetSellingPagesAsync(int productId, CancellationToken ct)
    {
        var pages = await _context.Set<LandingPage>()
            .AsNoTracking()
            .Where(lp => lp.ProductId == productId && lp.Status != LandingPageStatus.Archived)
            .OrderBy(lp => lp.Status == LandingPageStatus.Published ? 0 : 1)
            .ThenBy(lp => lp.Title)
            .ThenBy(lp => lp.Id)
            .Select(lp => new { lp.PublicId, lp.Title, lp.Status })
            .ToListAsync(ct);

        return pages.Select(lp => new ProductSellingPageDto(lp.PublicId, lp.Title, lp.Status.ToString())).ToList();
    }
}
