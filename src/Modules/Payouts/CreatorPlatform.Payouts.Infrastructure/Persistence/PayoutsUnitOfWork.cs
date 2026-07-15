using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Payouts.Infrastructure.Persistence;

public sealed class PayoutsUnitOfWork : IPayoutsUnitOfWork
{
    // Namespace for pg_advisory_xact_lock(namespace, creatorId) — arbitrary constant, just needs to not
    // collide with other advisory lock usages in this database.
    private const int PayoutLockNamespace = 42001;

    private readonly CreatorPlatformDbContext _context;

    public PayoutsUnitOfWork(CreatorPlatformDbContext context)
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

    public async Task AcquireCreatorPayoutLockAsync(int creatorId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({PayoutLockNamespace}, {creatorId})", ct);
    }
}
