namespace CreatorPlatform.Payments.Domain;

public sealed class WebhookFailure
{
    private WebhookFailure()
    {
    }

    public static WebhookFailure Create(
        string provider,
        string eventId,
        string eventType,
        string payload,
        string errorMessage,
        DateTimeOffset occurredAt)
    {
        return new WebhookFailure
        {
            Provider = provider,
            EventId = eventId,
            EventType = eventType,
            Payload = payload,
            ErrorMessage = errorMessage,
            OccurredAt = occurredAt
        };
    }

    public int Id { get; private set; }

    /// <summary>Payment provider that sent the webhook, e.g. "stripe".</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>Provider-assigned event ID for deduplication.</summary>
    public string EventId { get; private set; } = string.Empty;

    /// <summary>Event type, e.g. "checkout.session.completed".</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Raw JSON payload received from the provider.</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Exception message or error description.</summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public bool IsResolved { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public void MarkResolved(DateTimeOffset resolvedAt)
    {
        IsResolved = true;
        ResolvedAt = resolvedAt;
    }
}
