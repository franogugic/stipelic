namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record ContactRow(
    string Email,
    DateTimeOffset FirstCapturedAt,
    int SourcesCount,
    string Sources,
    bool IsUnsubscribed,
    List<ContactSourceRow> SourceList);

public sealed record ContactSourceRow(Guid LandingPagePublicId, string Title);

public sealed record ContactStatsCountsRow(int Total, int Active, int NewThisMonth, int Unsubscribed);

/// <param name="Month">"yyyy-MM".</param>
public sealed record ContactGrowthRow(string Month, int Total);

public sealed record ContactSourceCountRow(Guid LandingPagePublicId, string Title, int Count);

/// <summary>Cross-landing-page contact directory for one creator — reads the materialized
/// <c>marketing.contact_summaries</c> table (kept up to date incrementally at capture time; see
/// <c>EmailCaptureService</c>), never <c>analytics.email_captures</c> directly, so this never
/// re-aggregates the creator's full capture history on a page load.</summary>
public interface IContactsRepository
{
    /// <summary>Returns up to <paramref name="limit"/> + 1 rows ordered by <c>Email</c> ascending (keyset
    /// pagination) — the extra row lets the caller detect a next page without a separate COUNT query,
    /// then trims it before returning to the API. <paramref name="landingPageId"/> (internal id, already
    /// ownership-checked by the caller) restricts the page to contacts captured on that landing page.</summary>
    Task<List<ContactRow>> SearchAsync(
        int creatorId, string? search, int? landingPageId, string? afterEmail, int limit, CancellationToken ct);

    /// <summary>Headline counts in one scan of the creator's summaries (plus a count of their opt-outs).
    /// <paramref name="monthStart"/> is the inclusive lower bound for <c>NewThisMonth</c>.</summary>
    Task<ContactStatsCountsRow> GetStatsCountsAsync(int creatorId, DateTimeOffset monthStart, CancellationToken ct);

    /// <summary>One row per calendar month (UTC) from <paramref name="windowStart"/> through
    /// <paramref name="lastMonthStart"/>, oldest first, each with the cumulative number of contacts first
    /// captured before that month ended — contacts from before the window are folded into the first month,
    /// so the running total starts from the creator's real base. Both bounds must be UTC month starts.</summary>
    Task<List<ContactGrowthRow>> GetGrowthAsync(
        int creatorId, DateTimeOffset windowStart, DateTimeOffset lastMonthStart, CancellationToken ct);

    /// <summary>Contact count per landing page (archived pages included) for every page with at least one
    /// contact, ordered by count descending, then title.</summary>
    Task<List<ContactSourceCountRow>> GetSourceCountsAsync(int creatorId, CancellationToken ct);
}
