using CreatorPlatform.Orders.Application.Dtos;

namespace CreatorPlatform.Orders.Application.Interfaces;

/// <summary>Short-lived cache of the dashboard home summary per creator. Read or written only after the caller's
/// ownership of the creator was checked — the key is the internal creator id, never anything the caller supplies.
/// Never throws for an unavailable cache: a read is then a miss, a write or remove does nothing.</summary>
public interface IHomeSummaryCache
{
    ValueTask<HomeSummaryDto?> GetAsync(int creatorId, CancellationToken ct);
    Task SetAsync(int creatorId, HomeSummaryDto value, CancellationToken ct);
    Task RemoveAsync(int creatorId, CancellationToken ct);
}
