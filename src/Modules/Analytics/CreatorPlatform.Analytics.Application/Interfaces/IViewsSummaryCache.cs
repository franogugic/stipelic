using CreatorPlatform.Analytics.Application.Dtos;

namespace CreatorPlatform.Analytics.Application.Interfaces;

/// <summary>Short-lived cache of per-landing-page view counts per creator. Read or written only after the caller's
/// ownership of the creator was checked — the key is the internal creator id, never anything the caller supplies.
/// Never throws for an unavailable cache: a read is then a miss, a write or remove does nothing.</summary>
public interface IViewsSummaryCache
{
    ValueTask<List<LandingPageViewsSummaryDto>?> GetAsync(int creatorId, CancellationToken ct);
    Task SetAsync(int creatorId, List<LandingPageViewsSummaryDto> value, CancellationToken ct);
    Task RemoveAsync(int creatorId, CancellationToken ct);
}
