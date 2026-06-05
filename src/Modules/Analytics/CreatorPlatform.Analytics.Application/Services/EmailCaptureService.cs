using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.EmailCaptures;

namespace CreatorPlatform.Analytics.Application.Services;

public sealed class EmailCaptureService : IEmailCaptureService
{
    private readonly IEmailCaptureRepository _repository;

    public EmailCaptureService(IEmailCaptureRepository repository)
    {
        _repository = repository;
    }

    public async Task CaptureAsync(int landingPageId, int? productId, string email, CancellationToken ct)
    {
        var capture = new EmailCapture
        {
            Id = Guid.NewGuid(),
            LandingPageId = landingPageId,
            ProductId = productId,
            Email = email.Trim().ToLowerInvariant(),
            CapturedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddAsync(capture, ct);
    }

    public Task<long> GetCaptureCountAsync(int landingPageId, CancellationToken ct) =>
        _repository.GetCaptureCountAsync(landingPageId, ct);
}
