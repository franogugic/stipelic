using CreatorPlatform.Orders.Application.Dtos;

namespace CreatorPlatform.Orders.Application.Interfaces;

/// <summary>Short-lived cache of dashboard trends per (creator, range). Read only after the caller's ownership
/// of the creator was checked — the key is the internal creator id, never anything the caller supplies.</summary>
public interface IDashboardTrendsCache
{
    bool TryGet(int creatorId, string range, out DashboardTrendsDto? value);
    void Set(int creatorId, string range, DashboardTrendsDto value);
}
