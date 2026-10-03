using CreatorPlatform.LandingPages.Domain.LandingPages;
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
}
