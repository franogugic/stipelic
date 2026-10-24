using Azure;
using CreatorPlatform.Email.Application.Exceptions;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure.Outbox;

/// <summary><see cref="Claimed"/> messages were sent or settled; with none, <see cref="IdleFor"/> says how long to wait.</summary>
public sealed record OutboxDispatchResult(int Claimed, TimeSpan IdleFor);

/// <summary>
/// One pass over the outbox:
/// <list type="number">
/// <item>Ask the <see cref="EmailSendThrottle"/> how much may be sent now; nothing is claimed that cannot be sent at once,
/// so a claimed message never waits out its lease.</item>
/// <item>Claim transactional mail first (oldest first), then fill the batch with campaign mail in fair-queue order
/// (<see cref="EmailOutboxMessage.QueuePosition"/>), leaving the transactional reserve. <c>FOR UPDATE SKIP LOCKED</c>
/// lets several Worker instances claim side by side; a claim is a lease, so a crashed instance's messages come back.</item>
/// <item>Send them in parallel (<see cref="EmailOutboxOptions.MaxParallelSends"/>), each in its own scope, and record
/// each outcome. Claimed messages are always finished, also when the Worker is stopping.</item>
/// </list>
/// </summary>
public sealed class EmailOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    EmailSendThrottle throttle,
    IOptions<EmailOutboxOptions> outboxOptions,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider,
    ILogger<EmailOutboxDispatcher> logger)
{
    public const int MaxRetryCount = 3;

    /// <summary>A transactional email (order access, lead magnet, verification, password reset, email change, payout) that
    /// will never arrive: logged at Error with this event, for alerting. Carries the purpose and the correlation key (the
    /// order, sign-up, user or payout it belongs to), never the address.</summary>
    public static readonly EventId TransactionalEmailFailed = new(7101, nameof(TransactionalEmailFailed));

    /// <summary>A campaign email that will never arrive: Warning, since bounces within a list are expected.</summary>
    public static readonly EventId CampaignEmailFailed = new(7102, nameof(CampaignEmailFailed));

    private const int ThrottledStatusCode = 429;
    private static readonly TimeSpan MinIdle = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromHours(1);

    private enum Outcome
    {
        Sent,
        Deferred,
        Retrying,
        Failed,
        Skipped,
        Unrecorded
    }

    public async Task<OutboxDispatchResult> DispatchOnceAsync(CancellationToken stoppingToken)
    {
        var settings = outboxOptions.Value;
        var budget = throttle.GetBudget();
        if (budget.Transactional == 0)
            return new OutboxDispatchResult(0, IdleUntil(budget.NextPermitAt));

        var messages = await ClaimAsync(budget, stoppingToken);
        if (messages.Count == 0)
            return new OutboxDispatchResult(0, settings.PollInterval);

        throttle.Record(messages.Count);

        var outcomes = new Outcome[messages.Count];
        // Not the stopping token: what is claimed is finished (bounded by the send timeout), never left to its lease.
        await Parallel.ForEachAsync(
            Enumerable.Range(0, messages.Count),
            new ParallelOptions { MaxDegreeOfParallelism = settings.MaxParallelSends },
            async (index, _) => outcomes[index] = await ProcessAsync(messages[index]));

        logger.LogInformation(
            "Email outbox pass: {Claimed} claimed ({Transactional} transactional): {Sent} sent, {Deferred} deferred, {Retrying} to retry, {Failed} failed, {Skipped} skipped.",
            messages.Count,
            messages.Count(message => message.IsTransactional),
            outcomes.Count(outcome => outcome == Outcome.Sent),
            outcomes.Count(outcome => outcome == Outcome.Deferred),
            outcomes.Count(outcome => outcome == Outcome.Retrying),
            outcomes.Count(outcome => outcome == Outcome.Failed),
            outcomes.Count(outcome => outcome is Outcome.Skipped or Outcome.Unrecorded));

        return new OutboxDispatchResult(messages.Count, TimeSpan.Zero);
    }

    private TimeSpan IdleUntil(DateTimeOffset? nextPermitAt)
    {
        var poll = outboxOptions.Value.PollInterval;
        if (nextPermitAt is null)
            return poll;

        var wait = nextPermitAt.Value - timeProvider.GetUtcNow();
        return wait < MinIdle ? MinIdle : wait > poll ? poll : wait;
    }

    private async Task<List<EmailOutboxMessage>> ClaimAsync(SendBudget budget, CancellationToken ct)
    {
        var settings = outboxOptions.Value;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>();
        var now = timeProvider.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Each WHERE repeats its partial index's filter (EmailOutboxMessageConfiguration) word for word.
        var transactionalLimit = Math.Min(settings.BatchSize, budget.Transactional);
        var transactional = await db.Set<EmailOutboxMessage>()
            .FromSql($"""
                SELECT * FROM email.email_outbox_messages
                WHERE "Purpose" <> 'CampaignBroadcast' AND "Status" IN ('Pending', 'Processing')
                  AND (("Status" = 'Pending' AND "NextAttemptAt" <= {now})
                       OR ("Status" = 'Processing' AND "ProcessingExpiresAt" <= {now}))
                ORDER BY "CreatedAt", "Id"
                LIMIT {transactionalLimit}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        var campaignLimit = Math.Min(
            settings.BatchSize - transactional.Count,
            Math.Min(budget.Campaign, budget.Transactional - transactional.Count));
        var campaign = campaignLimit <= 0
            ? []
            : await db.Set<EmailOutboxMessage>()
                .FromSql($"""
                    SELECT * FROM email.email_outbox_messages
                    WHERE "Purpose" = 'CampaignBroadcast' AND "Status" IN ('Pending', 'Processing')
                      AND (("Status" = 'Pending' AND "NextAttemptAt" <= {now})
                           OR ("Status" = 'Processing' AND "ProcessingExpiresAt" <= {now}))
                    ORDER BY "QueuePosition", "Id"
                    LIMIT {campaignLimit}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

        var claimed = transactional.Concat(campaign).ToList();
        var leaseUntil = now + settings.ProcessingLease;
        foreach (var message in claimed)
            message.MarkAsProcessing(leaseUntil);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return claimed;
    }

    private async Task<Outcome> ProcessAsync(EmailOutboxMessage message)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>();
        db.Attach(message);

        try
        {
            try
            {
                if (await IsCancelledAsync(db, message.Id))
                    return Skip(db, message);

                // Only sent mail ever loses its bodies (maintenance), and sent mail is never claimed again.
                if (message.HtmlBody is null || message.PlainTextBody is null)
                    throw new InvalidOperationException($"Email outbox message {message.Id} has no body to send.");

                logger.LogDebug(
                    "Sending email outbox message {MessageId}. Purpose: {Purpose}. CorrelationKey: {CorrelationKey}.",
                    message.Id, message.Purpose, message.CorrelationKey);

                using (var timeout = new CancellationTokenSource(outboxOptions.Value.SendTimeout, timeProvider))
                {
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    await sender.SendAsync(
                        message.ToEmail, message.Subject, message.HtmlBody, message.PlainTextBody,
                        message.ReplyTo, message.ListUnsubscribeUrl, timeout.Token);
                }

                message.MarkAsSent(timeProvider.GetUtcNow());
                await db.SaveChangesAsync(CancellationToken.None);
                return Outcome.Sent;
            }
            catch (Exception exception)
            {
                return await HandleFailureAsync(scope.ServiceProvider, db, message, exception);
            }
        }
        catch (Exception exception)
        {
            // The outcome could not be stored (e.g. the database went away): the lease runs out and the message is
            // claimed again, so it may go twice; nothing is lost.
            logger.LogError(exception,
                "Email outbox message {MessageId}: the send outcome could not be recorded; it is retried when its lease expires.",
                message.Id);
            return Outcome.Unrecorded;
        }
    }

    private async Task<Outcome> HandleFailureAsync(
        IServiceProvider services, CreatorPlatformDbContext db, EmailOutboxMessage message, Exception exception)
    {
        if (await IsCancelledAsync(db, message.Id))
            return Skip(db, message);

        var now = timeProvider.GetUtcNow();

        // "Try later" (provider throttling, provider unreachable): not a failed attempt. This instance pauses all sending
        // until then, and the message keeps its place (and its retries).
        if (TryGetRetryAfter(exception, out var retryAfter))
        {
            var until = now + retryAfter;
            throttle.PauseUntil(until);
            message.Reschedule(until);
            await db.SaveChangesAsync(CancellationToken.None);

            logger.LogWarning(exception,
                "Email outbox message {MessageId} deferred until {RetryAt}: the provider throttled or is unreachable ({Reason}). Sending is paused until then; no retry is used.",
                message.Id, until, exception.GetType().Name);
            return Outcome.Deferred;
        }

        var nextAttemptAt = now + GetRetryDelay(message.RetryCount + 1);
        message.MarkAsFailed(exception.Message, nextAttemptAt, MaxRetryCount);

        if (message.Status != EmailOutboxMessageStatus.Failed)
        {
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning(exception,
                "Email outbox message {MessageId} ({Purpose}) failed, attempt {RetryCount} of {MaxRetryCount}; next attempt at {NextAttemptAt}.",
                message.Id, message.Purpose, message.RetryCount, MaxRetryCount, nextAttemptAt);
            return Outcome.Retrying;
        }

        if (message.IsTransactional)
            logger.LogError(TransactionalEmailFailed, exception,
                "Transactional email permanently failed: {Purpose} for {CorrelationKey} (message {MessageId}) after {RetryCount} attempt(s). The recipient did not get it.",
                message.Purpose, message.CorrelationKey, message.Id, message.RetryCount);
        else
            logger.LogWarning(CampaignEmailFailed, exception,
                "Campaign email permanently failed: {CorrelationKey} (message {MessageId}) after {RetryCount} attempt(s).",
                message.CorrelationKey, message.Id, message.RetryCount);

        // The purpose's side effect (e.g. refunding a campaign's usage counter) commits or rolls back with the status.
        await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None);
        var handler = services.GetServices<IEmailSendFailureHandler>().FirstOrDefault(h => h.Purpose == message.Purpose);
        if (handler is not null)
        {
            try
            {
                await handler.HandleAsync(message, CancellationToken.None);
            }
            catch (Exception handlerException)
            {
                logger.LogWarning(handlerException,
                    "Email send-failure handler threw for message {MessageId} (Purpose {Purpose}); continuing without its side effect.",
                    message.Id, message.Purpose);
            }
        }

        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        return Outcome.Failed;
    }

    private bool TryGetRetryAfter(Exception exception, out TimeSpan retryAfter)
    {
        switch (exception)
        {
            case RequestFailedException { Status: ThrottledStatusCode } throttled:
                retryAfter = ReadRetryAfter(throttled) ?? TimeSpan.FromMinutes(emailOptions.Value.ThrottleBackoffMinutes);
                return true;
            case TransientEmailSendException transient:
                retryAfter = transient.RetryAfter;
                return true;
            default:
                retryAfter = default;
                return false;
        }
    }

    /// <summary>The provider's <c>Retry-After</c> (seconds), kept between one second and one hour.</summary>
    private static TimeSpan? ReadRetryAfter(RequestFailedException exception)
    {
        var response = exception.GetRawResponse();
        if (response is null || !response.Headers.TryGetValue("Retry-After", out var value)
            || !int.TryParse(value, out var seconds))
            return null;

        var retryAfter = TimeSpan.FromSeconds(Math.Max(1, seconds));
        return retryAfter > MaxRetryAfter ? MaxRetryAfter : retryAfter;
    }

    private Outcome Skip(CreatorPlatformDbContext db, EmailOutboxMessage message)
    {
        db.Entry(message).State = EntityState.Detached;
        logger.LogInformation("Skipped cancelled email outbox message {MessageId}.", message.Id);
        return Outcome.Skipped;
    }

    private static async Task<bool> IsCancelledAsync(CreatorPlatformDbContext db, Guid messageId) =>
        await db.Set<EmailOutboxMessage>()
            .AsNoTracking()
            .Where(message => message.Id == messageId)
            .Select(message => message.Status)
            .SingleAsync(CancellationToken.None) == EmailOutboxMessageStatus.Cancelled;

    private static TimeSpan GetRetryDelay(int retryCount) => retryCount switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        _ => TimeSpan.FromMinutes(15)
    };
}
