using CreatorPlatform.Analytics.Domain.EmailCaptures;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IEmailCaptureRepository
{
    /// <summary>Idempotent insert (ON CONFLICT (LandingPageId, Email) DO NOTHING). Returns true only when
    /// a row was actually inserted — false on a duplicate — so the caller can gate contact-count usage
    /// tracking on real inserts only.</summary>
    Task<bool> AddAsync(EmailCapture capture, CancellationToken ct);
    Task<long> GetCaptureCountAsync(int landingPageId, CancellationToken ct);
    Task<List<EmailCapture>> ListByLandingPageIdAsync(int landingPageId, CancellationToken ct);
    Task<List<CapturesBucketRow>> GetBucketedCapturesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct);

    /// <summary>Upserts the (creatorId, email) row in <c>marketing.contact_summaries</c> — call only when
    /// <see cref="AddAsync"/> actually inserted a new capture row. Idempotent: a second capture of the
    /// same email on the same landing page updates <c>LastCapturedAt</c> only (the landing page id is
    /// already in the array); a capture on a landing page not yet in the array appends it.</summary>
    Task UpsertContactSummaryAsync(int creatorId, int landingPageId, string email, DateTimeOffset capturedAt, CancellationToken ct);
}

public sealed record CapturesBucketRow(
    DateTimeOffset BucketStart,
    long CaptureCount);
