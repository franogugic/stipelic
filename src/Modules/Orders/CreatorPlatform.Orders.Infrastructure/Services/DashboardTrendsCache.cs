using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CreatorPlatform.Orders.Infrastructure.Services;

public sealed class DashboardTrendsCache : IDashboardTrendsCache
{
    // Short TTL instead of invalidation: a new sale or view shows up within a minute, and the two aggregate
    // queries behind a miss stay cheap. (Moves to Redis with the other caches in chapter 09.)
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    public DashboardTrendsCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryGet(int creatorId, string range, out DashboardTrendsDto? value)
    {
        return _cache.TryGetValue(Key(creatorId, range), out value);
    }

    public void Set(int creatorId, string range, DashboardTrendsDto value)
    {
        _cache.Set(Key(creatorId, range), value, Ttl);
    }

    private static string Key(int creatorId, string range) => $"dashboard-trends:{creatorId}:{range}";
}
