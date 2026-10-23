using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Payments.Infrastructure.Repositories;

public sealed class ProcessedWebhookEventStore : IProcessedWebhookEventStore
{
    private readonly CreatorPlatformDbContext _context;

    public ProcessedWebhookEventStore(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<WebhookEventClaim> TryClaimAsync(
        string provider, string eventId, string eventType, DateTimeOffset now, TimeSpan staleAfter, CancellationToken ct)
    {
        var staleBefore = now - staleAfter;

        // The unique key makes this one statement the whole race: a new event is inserted; an existing one is taken over
        // only when its claim went stale without being processed. A row back means this delivery owns the event.
        var claimed = await _context.Database.SqlQuery<string>($"""
            INSERT INTO payments.processed_webhook_events ("EventId", "Provider", "EventType", "ClaimedAt", "ProcessedAt")
            VALUES ({eventId}, {provider}, {eventType}, {now}, NULL)
            ON CONFLICT ("EventId") DO UPDATE SET "ClaimedAt" = EXCLUDED."ClaimedAt"
            WHERE processed_webhook_events."ProcessedAt" IS NULL AND processed_webhook_events."ClaimedAt" < {staleBefore}
            RETURNING "EventId" AS "Value"
            """).ToListAsync(ct);

        if (claimed.Count > 0)
            return WebhookEventClaim.Claimed;

        var processedAt = await _context.Database.SqlQuery<DateTimeOffset?>($"""
            SELECT "ProcessedAt" AS "Value" FROM payments.processed_webhook_events WHERE "EventId" = {eventId}
            """).ToListAsync(ct);

        // Released between the two statements: let the next delivery claim it.
        if (processedAt.Count == 0)
            return WebhookEventClaim.InProgress;

        return processedAt[0] is null ? WebhookEventClaim.InProgress : WebhookEventClaim.AlreadyProcessed;
    }

    public async Task MarkProcessedAsync(string eventId, DateTimeOffset processedAt, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlAsync($"""
            UPDATE payments.processed_webhook_events SET "ProcessedAt" = {processedAt} WHERE "EventId" = {eventId}
            """, ct);
    }

    public async Task ReleaseAsync(string eventId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlAsync($"""
            DELETE FROM payments.processed_webhook_events WHERE "EventId" = {eventId} AND "ProcessedAt" IS NULL
            """, ct);
    }
}
