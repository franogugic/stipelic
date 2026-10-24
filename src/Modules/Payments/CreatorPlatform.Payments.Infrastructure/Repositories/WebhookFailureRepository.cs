using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Domain;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Payments.Infrastructure.Repositories;

public sealed class WebhookFailureRepository : IWebhookFailureRepository
{
    private readonly CreatorPlatformDbContext _context;

    public WebhookFailureRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(WebhookFailure failure, CancellationToken ct)
    {
        await _context.Set<WebhookFailure>().AddAsync(failure, ct);
    }

    public async Task<WebhookFailure?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context.Set<WebhookFailure>().FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public async Task<WebhookFailure?> GetOpenHandlerFailureAsync(string provider, string eventId, CancellationToken ct)
    {
        return await _context.Set<WebhookFailure>()
            .Where(f => f.Provider == provider && f.EventId == eventId && !f.IsResolved && f.Kind == WebhookFailureKind.HandlerFailed)
            .OrderBy(f => f.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> ResolveOpenHandlerFailuresAsync(string provider, string eventId, DateTimeOffset resolvedAt, CancellationToken ct)
    {
        return await _context.Set<WebhookFailure>()
            .Where(f => f.Provider == provider && f.EventId == eventId && !f.IsResolved && f.Kind == WebhookFailureKind.HandlerFailed)
            .ExecuteUpdateAsync(set => set
                .SetProperty(f => f.IsResolved, true)
                .SetProperty(f => f.ResolvedAt, resolvedAt), ct);
    }

    public async Task<List<WebhookFailure>> ListAsync(bool? resolved, int limit, CancellationToken ct)
    {
        var query = _context.Set<WebhookFailure>().AsNoTracking();
        if (resolved is bool isResolved)
            query = query.Where(f => f.IsResolved == isResolved);

        return await query
            .OrderByDescending(f => f.LastFailedAt)
            .ThenByDescending(f => f.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
