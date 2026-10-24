namespace CreatorPlatform.Payments.Domain;

/// <summary>What kind of problem a <see cref="WebhookFailure"/> records.</summary>
public enum WebhookFailureKind
{
    /// <summary>The handler threw. <see cref="WebhookFailure.Payload"/> is the raw, once-verified event, so an admin can
    /// replay it; a later successful delivery of the same event resolves it automatically.</summary>
    HandlerFailed,

    /// <summary>The handler ran but found something only a person can fix (e.g. paid after switching to Free). The
    /// payload is a description, not an event: it is resolved by hand, never replayed.</summary>
    NeedsAttention
}

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
        DateTimeOffset occurredAt,
        WebhookFailureKind kind)
    {
        return new WebhookFailure
        {
            Provider = provider,
            EventId = eventId,
            EventType = eventType,
            Payload = payload,
            ErrorMessage = errorMessage,
            OccurredAt = occurredAt,
            LastFailedAt = occurredAt,
            Attempts = 1,
            Kind = kind
        };
    }

    public int Id { get; private set; }

    /// <summary>Payment provider that sent the webhook, e.g. "stripe".</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>Provider-assigned event ID for deduplication.</summary>
    public string EventId { get; private set; } = string.Empty;

    /// <summary>Event type, e.g. "checkout.session.completed".</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Raw JSON payload received from the provider (<see cref="WebhookFailureKind.HandlerFailed"/>), or a
    /// description of what needs fixing (<see cref="WebhookFailureKind.NeedsAttention"/>).</summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>Exception message or error description — the latest attempt's.</summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    public WebhookFailureKind Kind { get; private set; }

    /// <summary>When the first attempt failed.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When the latest attempt failed (Stripe's retries and admin replays count).</summary>
    public DateTimeOffset LastFailedAt { get; private set; }

    public int Attempts { get; private set; }

    public bool IsResolved { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Another failed delivery or replay of the same event.</summary>
    public void RecordAttempt(string errorMessage, DateTimeOffset failedAt)
    {
        if (IsResolved)
            throw new InvalidOperationException($"Webhook failure {Id} is resolved; record a new one instead.");

        ErrorMessage = errorMessage;
        LastFailedAt = failedAt;
        Attempts++;
    }

    public void MarkResolved(DateTimeOffset resolvedAt)
    {
        if (IsResolved)
            return;

        IsResolved = true;
        ResolvedAt = resolvedAt;
    }
}
