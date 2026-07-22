using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Media.Infrastructure.Persistence;

public sealed class MediaUnitOfWork : IMediaUnitOfWork
{
    private readonly CreatorPlatformDbContext _context;

    public MediaUnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
