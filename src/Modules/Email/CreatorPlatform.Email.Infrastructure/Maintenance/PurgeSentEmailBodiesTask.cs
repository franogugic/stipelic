using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure.Maintenance;

/// <summary>
/// Drops the HTML and text bodies of mail sent more than <see cref="MaintenanceOptions.SentEmailBodyRetentionDays"/> ago
/// (a campaign keeps one full body per recipient: about 200 MB for 10,000 recipients). The row stays, so delivery
/// history, campaign progress and open/click statistics are unchanged. Pending, failed and cancelled messages keep their
/// bodies (a failed one can still be resent).
/// </summary>
public sealed class PurgeSentEmailBodiesTask(
    CreatorPlatformDbContext db,
    IOptions<MaintenanceOptions> options) : IMaintenanceTask
{
    public string Name => "sent-email-bodies";

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var settings = options.Value;
        var sentBefore = now.AddDays(-settings.SentEmailBodyRetentionDays);
        var messages = db.Set<EmailOutboxMessage>();
        var total = 0;

        while (true)
        {
            var batch = Candidates(db, sentBefore)
                .OrderBy(message => message.SentAt)
                .Take(settings.BatchSize)
                .Select(message => message.Id);

            var purged = await messages
                .Where(message => batch.Contains(message.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.HtmlBody, (string?)null)
                    .SetProperty(message => message.PlainTextBody, (string?)null), ct);

            total += purged;
            if (purged < settings.BatchSize)
                return total;
        }
    }

    /// <summary>Sent before <paramref name="sentBefore"/> and still holding a body. The predicate repeats the partial
    /// index's filter (<c>"Status" = 'Sent' AND "HtmlBody" IS NOT NULL</c>; the enum is a constant, so it is inlined as a
    /// literal), which is what lets Postgres answer it from IX_email_outbox_messages_SentAt_WithBodies.</summary>
    public static IQueryable<EmailOutboxMessage> Candidates(CreatorPlatformDbContext db, DateTimeOffset sentBefore) =>
        db.Set<EmailOutboxMessage>()
            .Where(message => message.Status == EmailOutboxMessageStatus.Sent
                              && message.HtmlBody != null
                              && message.SentAt < sentBefore);
}
