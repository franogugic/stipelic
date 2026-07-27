using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Persistence;

public sealed class MarketingUnitOfWork : IMarketingUnitOfWork
{
    // Namespace for pg_advisory_xact_lock(namespace, creatorId) — distinct from the Payouts module's
    // 42001 so the two locks never collide, even for the same creator id.
    private const int CampaignLockNamespace = 42002;

    private readonly CreatorPlatformDbContext _context;

    public MarketingUnitOfWork(CreatorPlatformDbContext context)
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

    public async Task AcquireCreatorCampaignLockAsync(int creatorId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({CampaignLockNamespace}, {creatorId})", ct);
    }

    public async Task ReloadAsync(object entity, CancellationToken ct)
    {
        await _context.Entry(entity).ReloadAsync(ct);
    }
}
