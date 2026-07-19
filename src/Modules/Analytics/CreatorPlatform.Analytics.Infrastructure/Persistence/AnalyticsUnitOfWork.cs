using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Analytics.Infrastructure.Persistence;

public sealed class AnalyticsUnitOfWork : IAnalyticsUnitOfWork
{
    private readonly CreatorPlatformDbContext _context;

    public AnalyticsUnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
