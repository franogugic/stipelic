namespace CreatorPlatform.Payments.Domain;

/// <summary>
/// One Stripe event id, claimed when its handling starts and stamped when it succeeded — so a redelivered or concurrently
/// delivered event runs its handler once. Written only through <c>IProcessedWebhookEventStore</c>'s single-statement claim;
/// the entity exists for the table's mapping.
/// </summary>
public sealed class ProcessedWebhookEvent
{
    private ProcessedWebhookEvent()
    {
    }

    /// <summary>The provider's event id — the primary key: an event is claimed once.</summary>
    public string EventId { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string EventType { get; private set; } = string.Empty;

    /// <summary>When the current claim was taken. A claim never stamped as processed is taken over once it is stale (the
    /// process died mid-handling), so the event is not lost.</summary>
    public DateTimeOffset ClaimedAt { get; private set; }

    /// <summary>Null while being handled; set when the handler succeeded.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }
}
