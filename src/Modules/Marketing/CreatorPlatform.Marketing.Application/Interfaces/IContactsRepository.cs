namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record ContactRow(string Email, DateTimeOffset FirstCapturedAt, int SourcesCount, string Sources, bool IsUnsubscribed);

/// <summary>Cross-landing-page contact directory for one creator — reads the materialized
/// <c>marketing.contact_summaries</c> table (kept up to date incrementally at capture time; see
/// <c>EmailCaptureService</c>), never <c>analytics.email_captures</c> directly, so this never
/// re-aggregates the creator's full capture history on a page load.</summary>
public interface IContactsRepository
{
    /// <summary>Returns up to <paramref name="limit"/> + 1 rows ordered by <c>Email</c> ascending (keyset
    /// pagination) — the extra row lets the caller detect a next page without a separate COUNT query,
    /// then trims it before returning to the API.</summary>
    Task<List<ContactRow>> SearchAsync(int creatorId, string? search, string? afterEmail, int limit, CancellationToken ct);
}
