using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Shared.Infrastructure.Maintenance;

public sealed record MaintenanceTaskResult(string Name, int Affected, bool Succeeded);

/// <summary><see cref="Ran"/> is false when another instance held the lock and nothing was done.</summary>
public sealed record MaintenanceRunResult(bool Ran, IReadOnlyList<MaintenanceTaskResult> Tasks)
{
    public static readonly MaintenanceRunResult Skipped = new(false, []);
}

/// <summary>
/// Runs every registered <see cref="IMaintenanceTask"/> once. Several Worker replicas may call it at the same time: a
/// Postgres session advisory lock lets exactly one of them run, the others skip. One task failing is logged as an Error
/// and does not stop the others; it runs again on the next run.
/// </summary>
public sealed class MaintenanceRunner(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<MaintenanceRunner> logger)
{
    /// <summary>The advisory lock key ("LUMAMNT" + 1). Unique to this job in the database.</summary>
    public const long AdvisoryLockKey = 0x4C554D414D4E5401;

    public async Task<MaintenanceRunResult> RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>();

        // One connection for the whole run: a session advisory lock belongs to the connection that took it.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await TryLockAsync(db, ct))
            {
                logger.LogDebug("Maintenance skipped: another Worker instance is running it.");
                return MaintenanceRunResult.Skipped;
            }

            try
            {
                return new MaintenanceRunResult(true, await RunTasksAsync(scope.ServiceProvider, db, ct));
            }
            finally
            {
                await UnlockAsync(db);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<List<MaintenanceTaskResult>> RunTasksAsync(
        IServiceProvider services, CreatorPlatformDbContext db, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var results = new List<MaintenanceTaskResult>();

        foreach (var task in services.GetServices<IMaintenanceTask>())
        {
            try
            {
                var affected = await task.RunAsync(now, ct);
                results.Add(new MaintenanceTaskResult(task.Name, affected, Succeeded: true));

                if (affected > 0)
                    logger.LogInformation("Maintenance task {MaintenanceTask}: {Affected} row(s).", task.Name, affected);
                else
                    logger.LogDebug("Maintenance task {MaintenanceTask}: nothing to do.", task.Name);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Whatever the task left tracked must not leak into the next one.
                db.ChangeTracker.Clear();
                results.Add(new MaintenanceTaskResult(task.Name, 0, Succeeded: false));
                logger.LogError(exception,
                    "Maintenance task {MaintenanceTask} failed; the other tasks still run, and it is retried on the next run.",
                    task.Name);
            }
        }

        return results;
    }

    private static async Task<bool> TryLockAsync(CreatorPlatformDbContext db, CancellationToken ct) =>
        await db.Database
            .SqlQuery<bool>($"SELECT pg_try_advisory_lock({AdvisoryLockKey}) AS \"Value\"")
            .SingleAsync(ct);

    private async Task UnlockAsync(CreatorPlatformDbContext db)
    {
        try
        {
            await db.Database
                .SqlQuery<bool>($"SELECT pg_advisory_unlock({AdvisoryLockKey}) AS \"Value\"")
                .SingleAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Closing the connection releases it as well (the pool resets the session).
            logger.LogWarning(exception, "Maintenance could not release its advisory lock explicitly.");
        }
    }
}
