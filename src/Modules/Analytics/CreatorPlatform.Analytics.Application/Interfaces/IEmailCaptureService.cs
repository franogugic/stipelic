namespace CreatorPlatform.Analytics.Application.Interfaces;

using CreatorPlatform.Analytics.Application.Dtos;

public interface IEmailCaptureService
{
    Task CaptureAsync(int landingPageId, int? productId, string email, CancellationToken ct);
    Task<long> GetCaptureCountAsync(int landingPageId, CancellationToken ct);
    Task<List<EmailCaptureResponseDto>> ListCapturesAsync(int landingPageId, CancellationToken ct);
}
