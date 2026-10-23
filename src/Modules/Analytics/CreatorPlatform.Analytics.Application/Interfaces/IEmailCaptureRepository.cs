using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.Shared.Application.Analytics;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IEmailCaptureRepository
{
    /// <summary>Idempotent insert (ON CONFLICT (LandingPageId, Email) DO NOTHING). Returns true only when
    /// a row was actually inserted — false on a duplicate — so the caller can gate contact-count usage
    /// tracking on real inserts only.</summary>
    Task<bool> AddAsync(EmailCapture capture, CancellationToken ct);
    /// <summary>One aggregate over the page's captures with a FILTER per period, by <c>CapturedAt</c>.</summary>
    Task<CapturesByPeriodRow> GetCaptureCountsByPeriodAsync(int landingPageId, StatsPeriods periods, CancellationToken ct);

    /// <summary>Capture counts for many landing pages in one grouped query. Pages without captures are absent.</summary>
    Task<Dictionary<int, int>> GetCaptureCountsAsync(IReadOnlyCollection<int> landingPageIds, CancellationToken ct);
    /// <summary>The newest <paramref name="limit"/> captures of the page (CapturedAt DESC, served by the
    /// (LandingPageId, CapturedAt) index).</summary>
    /// <summary>The page's newest captures, each with whether that email has unsubscribed from the page's creator.</summary>
    Task<List<NewestCaptureRow>> ListNewestByLandingPageIdAsync(int landingPageId, int limit, CancellationToken ct);

    /// <summary>Upserts the (creatorId, email) row in <c>marketing.contact_summaries</c> — call only when
    /// <see cref="AddAsync"/> actually inserted a new capture row. Idempotent: a second capture of the
    /// same email on the same landing page updates <c>LastCapturedAt</c> only (the landing page id is
    /// already in the array); a capture on a landing page not yet in the array appends it. Returns true when
    /// the row was inserted — a contact this creator has never captured before — and false when an existing
    /// contact was updated.</summary>
    Task<bool> UpsertContactSummaryAsync(int creatorId, int landingPageId, string email, DateTimeOffset capturedAt, CancellationToken ct);

    /// <summary>Whether the creator's contact with this (normalised) email has unsubscribed.</summary>
    Task<bool> IsUnsubscribedAsync(int creatorId, string email, CancellationToken ct);
}

public sealed record NewestCaptureRow(string Email, DateTimeOffset CapturedAt, bool Unsubscribed);

public sealed record CapturesByPeriodRow(long Today, long Last7Days, long Last30Days, long AllTime);
