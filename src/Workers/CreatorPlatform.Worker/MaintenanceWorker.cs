using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Worker;

/// <summary>
/// Runs the cleanup tasks (<see cref="IMaintenanceTask"/>, registered by the modules) every
/// <see cref="MaintenanceOptions.IntervalMinutes"/>, first after <see cref="MaintenanceOptions.StartDelaySeconds"/>.
/// With several Worker replicas each one ticks, and the runner's advisory lock lets only one of them work per run.
/// </summary>
public sealed class MaintenanceWorker(
    MaintenanceRunner runner,
    IOptions<MaintenanceOptions> options,
    TimeProvider timeProvider,
    ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogWarning("Maintenance is disabled (Maintenance:Enabled=false): nothing is cleaned up by this Worker.");
            return;
        }

        logger.LogInformation(
            "Maintenance worker started. IntervalMinutes: {IntervalMinutes}. StartDelaySeconds: {StartDelaySeconds}.",
            settings.IntervalMinutes,
            settings.StartDelaySeconds);

        try
        {
            await Task.Delay(settings.StartDelay, timeProvider, stoppingToken);

            using var timer = new PeriodicTimer(settings.Interval, timeProvider);
            do
            {
                try
                {
                    await runner.RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // The run itself failed (e.g. the database is unreachable); the next tick tries again.
                    logger.LogError(exception, "Maintenance run failed; it is retried on the next run.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
