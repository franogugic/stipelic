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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Email outbox worker failed while processing messages.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>();
        var now = DateTimeOffset.UtcNow;

        var messages = await dbContext.Set<EmailOutboxMessage>()
            .Where(message =>
                message.Status == EmailOutboxMessageStatus.Pending &&
                message.NextAttemptAt <= now)
            .OrderBy(message => message.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return;

        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        foreach (var message in messages)
        {
            await ProcessMessageAsync(dbContext, emailSender, message, ct);
        }
    }

    private async Task ProcessMessageAsync(
        CreatorPlatformDbContext dbContext,
        IEmailSender emailSender,
        EmailOutboxMessage message,
        CancellationToken ct)
    {
        try
        {
            await emailSender.SendAsync(
                message.ToEmail,
                message.Subject,
                message.HtmlBody,
                message.PlainTextBody,
                ct);

            message.MarkAsSent(DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            var nextAttemptAt = DateTimeOffset.UtcNow.Add(GetRetryDelay(message.RetryCount + 1));
            message.MarkAsFailed(exception.Message, nextAttemptAt, MaxRetryCount);

            logger.LogWarning(
                exception,
                "Failed to send email outbox message {MessageId}. RetryCount: {RetryCount}",
                message.Id,
                message.RetryCount);
        }

        await dbContext.SaveChangesAsync(ct);
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
