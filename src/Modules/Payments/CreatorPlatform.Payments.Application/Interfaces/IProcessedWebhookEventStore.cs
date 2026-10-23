namespace CreatorPlatform.Payments.Application.Interfaces;

public enum WebhookEventClaim
{
    /// <summary>This delivery handles the event.</summary>
    Claimed,

    /// <summary>The event was handled successfully before: nothing to do.</summary>
    AlreadyProcessed,

    /// <summary>Another delivery is handling it right now.</summary>
    InProgress
}

/// <summary>
/// The idempotency store for webhook events (<c>payments.processed_webhook_events</c>). Each call is its own statement,
/// committed at once and outside any handler transaction: a claim must be visible to a concurrent delivery immediately.
/// </summary>
public interface IProcessedWebhookEventStore
{
    /// <summary>One atomic insert-or-take-over: claims the event unless it is processed, or claimed less than
    /// <paramref name="staleAfter"/> ago by a delivery that may still be running.</summary>
    Task<WebhookEventClaim> TryClaimAsync(
        string provider, string eventId, string eventType, DateTimeOffset now, TimeSpan staleAfter, CancellationToken ct);

    Task MarkProcessedAsync(string eventId, DateTimeOffset processedAt, CancellationToken ct);

    /// <summary>Gives a claim back after the handler failed, so Stripe's retry or an admin's replay can claim it again.</summary>
    Task ReleaseAsync(string eventId, CancellationToken ct);
}
