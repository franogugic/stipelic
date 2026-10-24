using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure.Outbox;

/// <summary>The Worker's outbox loop: pass after pass with no pause while messages are ready; when nothing is ready (or
/// the quota is used up), it waits for the poll interval or the next free permit, whichever comes first.</summary>
public sealed class EmailOutboxProcessor(
    EmailOutboxDispatcher dispatcher,
    IOptions<EmailOutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailOutboxProcessor> logger) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        logger.LogInformation(
            "Email outbox processor started. BatchSize: {BatchSize}. MaxParallelSends: {MaxParallelSends}. Quota per instance: {PerMinute}/min, {PerHour}/h, {Reserve}% kept for transactional mail.",
            settings.BatchSize, settings.MaxParallelSends, settings.PerMinute, settings.PerHour, settings.TransactionalReservePercent);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan idle;
            try
            {
                var result = await dispatcher.DispatchOnceAsync(stoppingToken);
                if (result.Claimed > 0)
                    continue;   // more may be ready: no pause while work remains
                idle = result.IdleFor;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Email outbox pass failed; trying again after the poll interval.");
                idle = options.Value.PollInterval;
            }

            try
            {
                await Task.Delay(idle, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
