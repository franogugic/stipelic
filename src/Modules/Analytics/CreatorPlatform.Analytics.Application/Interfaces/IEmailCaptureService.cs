namespace CreatorPlatform.Analytics.Application.Interfaces;

using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Shared.Application.Analytics;

public interface IEmailCaptureService
{
    /// <summary>Enforces the creator's plan max_contacts limit, which counts unique contacts: no active
    /// subscription, or a NEW contact when the limit is reached, both throw ConflictException (409) — the public
    /// capture page shows a generic "temporarily closed" message either way, never revealing why. The all-time
    /// max_contacts counter grows only when the sign-up creates a new contact for the creator; a known contact
    /// signing up on another page (or again on the same page) uses no slot and is never blocked.</summary>
    Task CaptureAsync(int landingPageId, int? productId, int creatorId, string email, CancellationToken ct);
    /// <summary>Captures of a landing page per analytics period. The caller has already checked ownership.</summary>
    Task<CapturesByPeriodRow> GetCaptureCountsByPeriodAsync(int landingPageId, StatsPeriods periods, CancellationToken ct);

    /// <summary>Capture counts for many landing pages in one grouped query, keyed by internal landing page id.
    /// Pages without captures are absent. The caller has already checked ownership of the pages.</summary>
    Task<Dictionary<int, int>> GetCaptureCountsAsync(IReadOnlyCollection<int> landingPageIds, CancellationToken ct);
    Task<List<EmailCaptureResponseDto>> ListCapturesAsync(int landingPageId, CancellationToken ct);
}
