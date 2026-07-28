namespace CreatorPlatform.Analytics.Application.Interfaces;

using CreatorPlatform.Analytics.Application.Dtos;

public interface IEmailCaptureService
{
    /// <summary>Enforces the creator's plan max_contacts limit before inserting: no active subscription,
    /// or the limit is already reached, both throw ConflictException (409) — the public capture page
    /// shows a generic "temporarily closed" message either way, never revealing why. The all-time
    /// contacts_total usage counter is incremented only when the insert actually adds a new row (a
    /// repeat signup for the same landing page + email is a no-op, not new usage).</summary>
    Task CaptureAsync(int landingPageId, int? productId, int creatorId, string email, CancellationToken ct);
    Task<long> GetCaptureCountAsync(int landingPageId, CancellationToken ct);
    Task<List<EmailCaptureResponseDto>> ListCapturesAsync(int landingPageId, CancellationToken ct);
}
