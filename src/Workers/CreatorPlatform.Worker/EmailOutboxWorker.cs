using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Worker;

public sealed class EmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxRetryCount = 3;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingLeaseTimeout = TimeSpan.FromMinutes(5);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email outbox worker started. BatchSize: {BatchSize}. PollingIntervalSeconds: {PollingIntervalSeconds}.",
            BatchSize,
            PollingInterval.TotalSeconds);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReadyMessagesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Email outbox worker failed while processing messages.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessReadyMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>();
        var messages = await ClaimReadyMessagesAsync(dbContext, ct);

        if (messages.Count == 0)
            return;

        logger.LogInformation(
            "Processing {MessageCount} pending email outbox message(s).",
            messages.Count);

        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        foreach (var message in messages)
        {
            await ProcessMessageAsync(dbContext, emailSender, message, ct);
        }
    }

    private static async Task<List<EmailOutboxMessage>> ClaimReadyMessagesAsync(
        CreatorPlatformDbContext dbContext,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var processingExpiresAt = now.Add(ProcessingLeaseTimeout);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        var messages = await dbContext.Set<EmailOutboxMessage>()
            .FromSqlInterpolated($"""
                SELECT *
                FROM email.email_outbox_messages
                WHERE (
                    ("Status" = 'Pending' AND "NextAttemptAt" <= {now})
                    OR ("Status" = 'Processing' AND "ProcessingExpiresAt" <= {now})
                )
                ORDER BY "CreatedAt"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            message.MarkAsProcessing(processingExpiresAt);
        }

        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return messages;
    }

    private async Task ProcessMessageAsync(
        CreatorPlatformDbContext dbContext,
        IEmailSender emailSender,
        EmailOutboxMessage message,
        CancellationToken ct)
    {
        try
        {
            if (await IsMessageCancelledAsync(dbContext, message.Id, ct))
            {
                dbContext.Entry(message).State = EntityState.Detached;

                logger.LogInformation(
                    "Skipped cancelled email outbox message {MessageId}.",
                    message.Id);

                return;
            }

            await emailSender.SendAsync(
                message.ToEmail,
                message.Subject,
                message.HtmlBody,
                message.PlainTextBody,
                ct);

            message.MarkAsSent(DateTimeOffset.UtcNow);
            logger.LogInformation(
                "Sent email outbox message {MessageId}.",
                message.Id);
        }
        catch (Exception exception)
        {
            var nextAttemptAt = DateTimeOffset.UtcNow.Add(GetRetryDelay(message.RetryCount + 1));
            message.MarkAsFailed(exception.Message, nextAttemptAt, MaxRetryCount);

            if (message.Status == EmailOutboxMessageStatus.Failed)
            {
                logger.LogError(
                    exception,
                    "Email outbox message {MessageId} permanently failed after {RetryCount} attempt(s).",
                    message.Id,
                    message.RetryCount);
            }
            else
            {
                logger.LogWarning(
                    exception,
                    "Email outbox message {MessageId} failed. RetryCount: {RetryCount}. NextAttemptAt: {NextAttemptAt}.",
                    message.Id,
                    message.RetryCount,
                    nextAttemptAt);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task<bool> IsMessageCancelledAsync(
        CreatorPlatformDbContext dbContext,
        Guid messageId,
        CancellationToken ct)
    {
        var status = await dbContext.Set<EmailOutboxMessage>()
            .AsNoTracking()
            .Where(message => message.Id == messageId)
            .Select(message => message.Status)
            .SingleAsync(ct);

        return status == EmailOutboxMessageStatus.Cancelled;
    }

    private static TimeSpan GetRetryDelay(int retryCount)
    {
        return retryCount switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(15)
        };
    }
}
