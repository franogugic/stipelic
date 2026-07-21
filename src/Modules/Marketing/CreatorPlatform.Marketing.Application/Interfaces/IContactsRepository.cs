namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record ContactRow(string Email, DateTimeOffset FirstCapturedAt, int SourcesCount, string Sources, bool IsUnsubscribed);

/// <summary>Cross-landing-page contact directory for one creator — derived read (no entity aggregate
/// backs "a contact"; it's computed from analytics.email_captures + marketing.unsubscribes).</summary>
public interface IContactsRepository
{
    /// <summary>Returns up to <paramref name="limit"/> + 1 rows ordered by <c>Email</c> ascending (keyset
    /// pagination) — the extra row lets the caller detect a next page without a separate COUNT query,
    /// then trims it before returning to the API.</summary>
    Task<List<ContactRow>> SearchAsync(int creatorId, string? search, string? afterEmail, int limit, CancellationToken ct);
}
