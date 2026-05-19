using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Auth.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CreatorPlatformDbContext _context;

    public UnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        return _context.SaveChangesAsync(ct);
    }
}
