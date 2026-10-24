using CreatorPlatform.Payments.Domain;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IWebhookFailureRepository
{
    Task AddAsync(WebhookFailure failure, CancellationToken ct);

    Task<WebhookFailure?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>The unresolved <see cref="WebhookFailureKind.HandlerFailed"/> row of this event, if any — another failed
    /// attempt is added to it instead of a new row per Stripe retry.</summary>
    Task<WebhookFailure?> GetOpenHandlerFailureAsync(string provider, string eventId, CancellationToken ct);

    /// <summary>Resolves this event's unresolved <see cref="WebhookFailureKind.HandlerFailed"/> rows in one statement
    /// (a later delivery or a replay succeeded). Rows that need a manual fix stay open. Returns how many were resolved.</summary>
    Task<int> ResolveOpenHandlerFailuresAsync(string provider, string eventId, DateTimeOffset resolvedAt, CancellationToken ct);

    /// <summary>Newest first. <paramref name="resolved"/> null lists both.</summary>
    Task<List<WebhookFailure>> ListAsync(bool? resolved, int limit, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
