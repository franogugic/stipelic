using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CreatorPlatform.Orders.Infrastructure.Services;

public sealed class HomeSummaryCache : IHomeSummaryCache
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public HomeSummaryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryGet(int creatorId, out HomeSummaryDto? value)
    {
        return _cache.TryGetValue(Key(creatorId), out value);
    }

    public void Set(int creatorId, HomeSummaryDto value)
    {
        _cache.Set(Key(creatorId), value, Ttl);
    }

    public void Remove(int creatorId)
    {
        _cache.Remove(Key(creatorId));
    }

    // Keyed by the internal creator id, resolved by the ownership check, so a request for someone else's slug
    // can neither read nor overwrite the owner's entry.
    private static string Key(int creatorId) => $"home-summary:{creatorId}";
}
