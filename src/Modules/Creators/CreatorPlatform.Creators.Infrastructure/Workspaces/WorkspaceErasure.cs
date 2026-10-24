using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Creators.Infrastructure.Workspaces;

public sealed class WorkspaceErasure(
    CreatorPlatformDbContext db,
    IEnumerable<IWorkspaceDataPurger> purgers,
    ISubscriptionCheckoutSessionService checkoutSessions,
    IWorkspaceFiles files,
    ILogger<WorkspaceErasure> logger) : IWorkspaceErasure
{
    public async Task<WorkspaceErasureOutcome> FinishAsync(int creatorId, DateTimeOffset now, CancellationToken ct)
    {
        var state = await db.Set<Creator>()
            .AsNoTracking()
            .Where(creator => creator.Id == creatorId)
            .Select(creator => new { creator.Status, creator.DeletedAt, creator.FilesDeletedAt })
            .SingleOrDefaultAsync(ct);
        if (state is null || state.Status != CreatorStatus.Disabled)
            return WorkspaceErasureOutcome.NotDeleted;

        if (state.DeletedAt is null && !await EraseDataAsync(creatorId, now, ct))
            return WorkspaceErasureOutcome.WaitingForSale;

        if (state.FilesDeletedAt is not null)
            return WorkspaceErasureOutcome.Done;

        try
        {
            await files.DeleteAllAsync(creatorId, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "The files of deleted workspace {CreatorId} could not be deleted; the maintenance worker retries.", creatorId);
            return WorkspaceErasureOutcome.FilesPending;
        }

        await db.Database.ExecuteSqlAsync($"""
            UPDATE creators.creators SET "FilesDeletedAt" = {now} WHERE "Id" = {creatorId} AND "FilesDeletedAt" IS NULL
            """, ct);
        return WorkspaceErasureOutcome.Done;
    }

    /// <summary>False when a sale is in the way (nothing is erased then).</summary>
    private async Task<bool> EraseDataAsync(int creatorId, DateTimeOffset now, CancellationToken ct)
    {
        // Their unpaid orders are deleted, so no session of theirs may stay payable. Outside the transaction, as stopping
        // the billing is: a Stripe failure throws and nothing is erased.
        var expired = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sessionId in await OpenSaleSessionsAsync(creatorId, ct))
        {
            if (await checkoutSessions.ExpireAsync(sessionId, ct) == CheckoutSessionExpireOutcome.AlreadyCompleted)
            {
                // Paid just now: the checkout.session.completed webhook settles the order first, so it is kept.
                logger.LogInformation(
                    "Erasing workspace {CreatorId} waits: its Checkout session {SessionId} was just paid.", creatorId, sessionId);
                return false;
            }

            expired.Add(sessionId);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM creators.creators WHERE "Id" = {creatorId} FOR UPDATE""", ct);
        var creator = await db.Set<Creator>().SingleAsync(c => c.Id == creatorId, ct);
        await db.Entry(creator).ReloadAsync(ct);   // the scope may have tracked it before the lock
        if (creator.DeletedAt is not null)
            return true;   // A parallel run got here first.

        // A checkout that started just before the workspace was disabled may have opened a session since.
        if ((await OpenSaleSessionsAsync(creatorId, ct)).Any(sessionId => !expired.Contains(sessionId)))
        {
            logger.LogInformation("Erasing workspace {CreatorId} waits: a sale started while it was being deleted.", creatorId);
            return false;
        }

        foreach (var purger in purgers.OrderBy(purger => purger.Order))
            await purger.PurgeAsync(creatorId, ct);

        creator.MarkDeleted(now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Deleted workspace {CreatorId}: its data is erased.", creatorId);
        return true;
    }

    private async Task<List<string>> OpenSaleSessionsAsync(int creatorId, CancellationToken ct) =>
        await db.Database.SqlQuery<string>($"""
            SELECT "StripeCheckoutSessionId" AS "Value" FROM orders.orders
            WHERE "CreatorId" = {creatorId} AND "Status" = 'Pending' AND "StripeCheckoutSessionId" IS NOT NULL
            """).ToListAsync(ct);
}
