using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.LandingPages.Infrastructure.Persistence;

public sealed class LandingPagesUnitOfWork : ILandingPagesUnitOfWork
{
    private readonly CreatorPlatformDbContext _context;

    public LandingPagesUnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
