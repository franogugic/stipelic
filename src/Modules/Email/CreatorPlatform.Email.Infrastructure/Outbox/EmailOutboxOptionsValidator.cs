using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure.Outbox;

/// <summary>Checked at the Worker's start (after the provider defaults).</summary>
public sealed class EmailOutboxOptionsValidator : IValidateOptions<EmailOutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOutboxOptions options)
    {
        var failures = new List<string>();

        if (options.BatchSize is < 1 or > 500)
            failures.Add("Email:Outbox:BatchSize must be between 1 and 500.");
        if (options.MaxParallelSends is < 1 or > 64)
            failures.Add("Email:Outbox:MaxParallelSends must be between 1 and 64.");
        if (options.PollIntervalSeconds is < 1 or > 60)
            failures.Add("Email:Outbox:PollIntervalSeconds must be between 1 and 60.");
        if (options.ProcessingLeaseMinutes is < 1 or > 60)
            failures.Add("Email:Outbox:ProcessingLeaseMinutes must be between 1 and 60.");
        if (options.SendTimeoutSeconds is < 5 or > 120)
            failures.Add("Email:Outbox:SendTimeoutSeconds must be between 5 and 120.");
        if (options.PerMinute is < 1 or > 100_000)
            failures.Add("Email:Outbox:MaxSendsPerMinute must be between 1 and 100000.");
        if (options.PerHour is < 1 or > 1_000_000)
            failures.Add("Email:Outbox:MaxSendsPerHour must be between 1 and 1000000.");
        if (options.PerHour < options.PerMinute)
            failures.Add("Email:Outbox:MaxSendsPerHour must be at least Email:Outbox:MaxSendsPerMinute.");
        if (options.TransactionalReservePercent is < 0 or > 50)
            failures.Add("Email:Outbox:TransactionalReservePercent must be between 0 and 50.");

        // A claimed batch must be done well within its lease, or another instance claims it again and mail goes twice.
        if (options is { BatchSize: > 0, MaxParallelSends: > 0 })
        {
            var rounds = (options.BatchSize + options.MaxParallelSends - 1) / options.MaxParallelSends;
            var worstCase = TimeSpan.FromSeconds((double)rounds * options.SendTimeoutSeconds);
            if (worstCase > options.ProcessingLease / 2)
                failures.Add(
                    $"Email:Outbox: a batch can take up to {worstCase.TotalSeconds:0} s (BatchSize / MaxParallelSends rounds x " +
                    $"SendTimeoutSeconds), more than half of ProcessingLeaseMinutes; lower the batch or raise the lease.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
