using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Creators.Infrastructure.Maintenance;

/// <summary>
/// Finishes what deleting a workspace left open, in two steps:
/// <list type="number">
/// <item>Every disabled workspace whose data or files are not erased yet goes through <see cref="IWorkspaceErasure"/>:
/// a deletion whose erasure failed or had to wait for a sale, a tombstone whose files could not be deleted, and the
/// workspaces disabled before deletion erased anything (B12.1), once.</item>
/// <item>A tombstone's Stripe connected account is deleted
/// <see cref="MaintenanceOptions.DeletedConnectAccountRetentionDays"/> after the workspace was deleted, once its balance
/// is zero; until then refunds and disputes of its sales still need it.</item>
/// </list>
/// Each workspace is tried once per run; one that fails is logged and retried on the next run.
/// </summary>
public sealed class FinishDeletedWorkspacesTask(
    CreatorPlatformDbContext db,
    IWorkspaceErasure erasure,
    IServiceProvider services,
    IOptions<MaintenanceOptions> maintenanceOptions,
    IOptions<StripeOptions> stripeOptions,
    ILogger<FinishDeletedWorkspacesTask> logger) : IMaintenanceTask
{
    private static int _stripeNotConfiguredLogged;

    public string Name => "deleted-workspaces";

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct) =>
        await EraseAsync(now, ct) + await DeleteConnectAccountsAsync(now, ct);

    private async Task<int> EraseAsync(DateTimeOffset now, CancellationToken ct)
    {
        var batchSize = maintenanceOptions.Value.BatchSize;
        var tried = new HashSet<int>();
        var finished = 0;

        while (true)
        {
            var batch = await db.Set<Creator>()
                .AsNoTracking()
                .Where(creator => creator.Status == CreatorStatus.Disabled
                                  && (creator.DeletedAt == null || creator.FilesDeletedAt == null)
                                  && !tried.Contains(creator.Id))
                .OrderBy(creator => creator.Id)
                .Take(batchSize)
                .Select(creator => creator.Id)
                .ToListAsync(ct);

            foreach (var creatorId in batch)
            {
                tried.Add(creatorId);
                try
                {
                    if (await erasure.FinishAsync(creatorId, now, ct) == WorkspaceErasureOutcome.Done)
                        finished++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception,
                        "Erasing deleted workspace {CreatorId} failed; it is retried on the next run.", creatorId);
                }
            }

            if (batch.Count < batchSize)
                return finished;
        }
    }

    private async Task<int> DeleteConnectAccountsAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stripeOptions.Value.SecretKey))
        {
            if (Interlocked.Exchange(ref _stripeNotConfiguredLogged, 1) == 0)
                logger.LogWarning("Connected accounts of deleted workspaces are not deleted: this Worker has no Stripe key (Stripe:SecretKey).");
            return 0;
        }

        // Resolved only now: the Stripe client cannot be created without a key, and that must not fail the other tasks.
        var connectAccounts = services.GetRequiredService<IConnectAccountService>();
        var deletedBefore = now.AddDays(-maintenanceOptions.Value.DeletedConnectAccountRetentionDays);
        var batchSize = maintenanceOptions.Value.BatchSize;
        var tried = new HashSet<int>();
        var deleted = 0;

        while (true)
        {
            var batch = await db.Set<Creator>()
                .AsNoTracking()
                .Where(creator => creator.DeletedAt < deletedBefore
                                  && creator.StripeConnectAccountId != null
                                  && creator.ConnectAccountDeletedAt == null
                                  && !tried.Contains(creator.Id))
                .OrderBy(creator => creator.Id)
                .Take(batchSize)
                .Select(creator => new { creator.Id, AccountId = creator.StripeConnectAccountId! })
                .ToListAsync(ct);

            foreach (var tombstone in batch)
            {
                tried.Add(tombstone.Id);
                try
                {
                    var outcome = await connectAccounts.DeleteIfBalanceIsZeroAsync(tombstone.AccountId, ct);
                    if (outcome == ConnectAccountDeletionOutcome.BalanceNotZero)
                    {
                        logger.LogInformation(
                            "The connected account {AccountId} of deleted workspace {CreatorId} still has a balance; it is kept and checked again on the next run.",
                            tombstone.AccountId, tombstone.Id);
                        continue;
                    }

                    await db.Database.ExecuteSqlAsync($"""
                        UPDATE creators.creators SET "ConnectAccountDeletedAt" = {now}
                        WHERE "Id" = {tombstone.Id} AND "ConnectAccountDeletedAt" IS NULL
                        """, ct);
                    deleted++;
                    logger.LogInformation(
                        "Deleted the connected account {AccountId} of deleted workspace {CreatorId} ({Outcome}).",
                        tombstone.AccountId, tombstone.Id, outcome);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception,
                        "Deleting the connected account {AccountId} of deleted workspace {CreatorId} failed; it is retried on the next run.",
                        tombstone.AccountId, tombstone.Id);
                }
            }

            if (batch.Count < batchSize)
                return deleted;
        }
    }
}
