using Microsoft.Extensions.Options;

namespace CreatorPlatform.Shared.Infrastructure.Maintenance;

/// <summary>Fails the Worker's start on retention values that would delete live data or never clean up.</summary>
public sealed class MaintenanceOptionsValidator : IValidateOptions<MaintenanceOptions>
{
    public const int MinStalePendingOrderHours = 24;

    /// <summary>Past Stripe's dispute window (about 120 days after a charge).</summary>
    public const int MinDeletedConnectAccountRetentionDays = 120;

    public ValidateOptionsResult Validate(string? name, MaintenanceOptions options)
    {
        var failures = new List<string>();

        if (options.IntervalMinutes is < 5 or > 1440)
            failures.Add("Maintenance:IntervalMinutes must be between 5 and 1440.");
        if (options.StartDelaySeconds is < 0 or > 3600)
            failures.Add("Maintenance:StartDelaySeconds must be between 0 and 3600.");
        if (options.BatchSize is < 10 or > 10_000)
            failures.Add("Maintenance:BatchSize must be between 10 and 10000.");
        if (options.SentEmailBodyRetentionDays is < 1 or > 3650)
            failures.Add("Maintenance:SentEmailBodyRetentionDays must be between 1 and 3650.");
        if (options.ExpiredAuthRecordRetentionDays is < 1 or > 3650)
            failures.Add("Maintenance:ExpiredAuthRecordRetentionDays must be between 1 and 3650.");
        if (options.AbandonedUploadMinutes is < 30 or > 10_080)
            failures.Add("Maintenance:AbandonedUploadMinutes must be between 30 and 10080.");
        if (options.StalePendingOrderHours is < MinStalePendingOrderHours or > 720)
            failures.Add($"Maintenance:StalePendingOrderHours must be between {MinStalePendingOrderHours} (a Stripe Checkout " +
                         "session can be paid until it expires, 24 h after creation by default) and 720.");
        if (options.DeletedConnectAccountRetentionDays is < MinDeletedConnectAccountRetentionDays or > 3650)
            failures.Add($"Maintenance:DeletedConnectAccountRetentionDays must be between {MinDeletedConnectAccountRetentionDays} " +
                         "(disputes can come up to about 120 days after a charge) and 3650.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
