using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Worker;

/// <summary>Same polling-loop shape as <see cref="EmailOutboxWorker"/>: one scope per tick, sequential
/// processing within the batch, one row's failure never stops the rest.</summary>
public sealed class ScheduledCampaignDispatchWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MarketingOptions> marketingOptions,
    ILogger<ScheduledCampaignDispatchWorker> logger) : BackgroundService
{
    private const int BatchSize = 20;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Scheduled campaign dispatch worker started. BatchSize: {BatchSize}. PollingIntervalSeconds: {PollingIntervalSeconds}.",
            BatchSize,
            marketingOptions.Value.ScheduledCampaignPollIntervalSeconds);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = TimeSpan.FromSeconds(marketingOptions.Value.ScheduledCampaignPollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchDueCampaignsAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled campaign dispatch worker failed while processing due campaigns.");
            }

            await Task.Delay(pollingInterval, stoppingToken);
        }
    }

    private async Task DispatchDueCampaignsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var campaignRepository = scope.ServiceProvider.GetRequiredService<ICampaignRepository>();
        var duePublicIds = await campaignRepository.GetDueScheduledPublicIdsAsync(BatchSize, ct);

        if (duePublicIds.Count == 0)
            return;

        logger.LogInformation("Dispatching {Count} due scheduled campaign(s).", duePublicIds.Count);

        var sendService = scope.ServiceProvider.GetRequiredService<ICampaignSendService>();

        foreach (var campaignPublicId in duePublicIds)
        {
            try
            {
                await sendService.DispatchScheduledAsync(campaignPublicId, ct);
            }
            catch (Exception exception)
            {
                // DispatchScheduledAsync already catches pipeline failures and marks the campaign Failed —
                // this is a last-resort guard against something unexpected (e.g. a DB connectivity blip)
                // so one bad row never stops the rest of the batch.
                logger.LogError(exception, "Unexpected error dispatching scheduled campaign {CampaignPublicId}.", campaignPublicId);
            }
        }
    }
}
