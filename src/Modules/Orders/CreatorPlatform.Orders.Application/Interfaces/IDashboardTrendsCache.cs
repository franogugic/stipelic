using CreatorPlatform.Orders.Application.Dtos;

namespace CreatorPlatform.Orders.Application.Interfaces;

/// <summary>Short-lived cache of dashboard trends per (creator, range). Read only after the caller's ownership
/// of the creator was checked — the key is the internal creator id, never anything the caller supplies.
/// Never throws for an unavailable cache: a read is then a miss, a write or remove does nothing.</summary>
public interface IDashboardTrendsCache
{
    ValueTask<DashboardTrendsDto?> GetAsync(int creatorId, string range, CancellationToken ct);
    Task SetAsync(int creatorId, string range, DashboardTrendsDto value, CancellationToken ct);

    /// <summary>Drops every cached range of the creator (e.g. after their timezone changed).</summary>
    Task RemoveAsync(int creatorId, CancellationToken ct);
}
