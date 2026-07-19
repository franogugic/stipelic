using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Creators.Infrastructure.Services;

public sealed class CreatorUsageService : ICreatorUsageService
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorUsageService(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    /// <summary>Single atomic UPSERT, safe under concurrent callers with no additional locking (unlike the
    /// campaign send path, the public capture path does NOT hold a creator-scoped advisory lock — two
    /// parallel captures must not be able to race past the limit or clobber each other's increment). The
    /// insert branch is itself conditioned on the limit (via the SELECT ... WHERE), and the conflict branch
    /// re-checks the limit against the row's current value — so a rowcount of 0 always means "exceeded",
    /// whichever branch would have fired.</summary>
    public async Task<bool> TryConsumeAsync(int creatorId, string usageKey, int amount, int limit, UsagePeriod period, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var (periodStart, periodEnd) = UsagePeriodResolver.Resolve(period, now);

        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO creators.creator_usage_counters
                ("CreatorId", "UsageKey", "UsedValue", "PeriodStart", "PeriodEnd", "CreatedAt", "UpdatedAt")
            SELECT {creatorId}, {usageKey}, {amount}, {periodStart}, {periodEnd}, {now}, {now}
            WHERE {limit} < 0 OR {amount} <= {limit}
            ON CONFLICT ("CreatorId", "UsageKey", "PeriodStart", "PeriodEnd")
            DO UPDATE SET
                "UsedValue" = creator_usage_counters."UsedValue" + {amount},
                "UpdatedAt" = {now}
            WHERE {limit} < 0 OR creator_usage_counters."UsedValue" + {amount} <= {limit}
            """, ct);

        return rowsAffected > 0;
    }

    public async Task<int> GetUsedAsync(int creatorId, string usageKey, UsagePeriod period, CancellationToken ct)
    {
        var (periodStart, periodEnd) = UsagePeriodResolver.Resolve(period, DateTimeOffset.UtcNow);

        return await _context.Set<CreatorUsageCounter>()
            .AsNoTracking()
            .Where(c =>
                c.CreatorId == creatorId &&
                c.UsageKey == usageKey &&
                c.PeriodStart == periodStart &&
                c.PeriodEnd == periodEnd)
            .Select(c => (int?)c.UsedValue)
            .FirstOrDefaultAsync(ct) ?? 0;
    }
}
