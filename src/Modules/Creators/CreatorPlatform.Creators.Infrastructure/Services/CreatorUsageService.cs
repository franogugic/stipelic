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

    public async Task<bool> TryConsumeAsync(int creatorId, string usageKey, int amount, int limit, UsagePeriod period, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var (periodStart, periodEnd) = UsagePeriodResolver.Resolve(period, now);

        var counter = await _context.Set<CreatorUsageCounter>()
            .FirstOrDefaultAsync(c =>
                c.CreatorId == creatorId &&
                c.UsageKey == usageKey &&
                c.PeriodStart == periodStart &&
                c.PeriodEnd == periodEnd, ct);

        if (counter is null)
        {
            var creator = await _context.Set<Creator>().FirstAsync(c => c.Id == creatorId, ct);
            counter = CreatorUsageCounter.Create(creator, usageKey, periodStart, periodEnd, now);
        }

        var succeeded = counter.TryAddUsage(amount, limit, now);
        if (succeeded && _context.Entry(counter).State == EntityState.Detached)
            await _context.Set<CreatorUsageCounter>().AddAsync(counter, ct);

        return succeeded;
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
