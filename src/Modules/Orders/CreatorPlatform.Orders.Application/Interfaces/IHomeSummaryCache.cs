using CreatorPlatform.Orders.Application.Dtos;

namespace CreatorPlatform.Orders.Application.Interfaces;

/// <summary>Short-lived cache of the dashboard home summary per creator. Read or written only after the caller's
/// ownership of the creator was checked — the key is the internal creator id, never anything the caller supplies.</summary>
public interface IHomeSummaryCache
{
    bool TryGet(int creatorId, out HomeSummaryDto? value);
    void Set(int creatorId, HomeSummaryDto value);
    void Remove(int creatorId);
}
