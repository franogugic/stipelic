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
}

public sealed record CapturesBucketRow(
    DateTimeOffset BucketStart,
    long CaptureCount);
