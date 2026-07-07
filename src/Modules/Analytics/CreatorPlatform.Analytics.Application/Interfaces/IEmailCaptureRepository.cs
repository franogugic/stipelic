using CreatorPlatform.Analytics.Domain.EmailCaptures;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IEmailCaptureRepository
{
    Task AddAsync(EmailCapture capture, CancellationToken ct);
    Task<long> GetCaptureCountAsync(int landingPageId, CancellationToken ct);
    Task<List<EmailCapture>> ListByLandingPageIdAsync(int landingPageId, CancellationToken ct);
    Task<List<CapturesBucketRow>> GetBucketedCapturesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct);
}

public sealed record CapturesBucketRow(
    DateTimeOffset BucketStart,
    long CaptureCount);
