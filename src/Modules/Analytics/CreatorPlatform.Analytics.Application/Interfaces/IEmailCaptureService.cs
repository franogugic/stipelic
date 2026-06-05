namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface IEmailCaptureService
{
    Task CaptureAsync(int landingPageId, int? productId, string email, CancellationToken ct);
}
