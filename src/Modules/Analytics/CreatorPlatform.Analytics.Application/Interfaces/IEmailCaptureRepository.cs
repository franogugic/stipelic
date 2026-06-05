using CreatorPlatform.Analytics.Domain.EmailCaptures;

namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IEmailCaptureRepository
{
    Task AddAsync(EmailCapture capture, CancellationToken ct);
}
