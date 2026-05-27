using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Creators.Infrastructure.Persistence;

public sealed class CreatorsUnitOfWork : ICreatorsUnitOfWork
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorsUnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        await operation();

        await transaction.CommitAsync(ct);
    }
}
