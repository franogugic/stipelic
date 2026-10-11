using System.Collections.Concurrent;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace CreatorPlatform.Orders.Infrastructure.Services;

public sealed class DashboardTrendsCache : IDashboardTrendsCache
{
    // Short TTL instead of invalidating on every sale: a new sale or view shows up within a minute, and the two
    // aggregate queries behind a miss stay cheap. (Moves to Redis with the other caches in chapter 09.)
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    // One token per creator ties all of their ranges together, so Remove drops every range without knowing them.
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _creatorTokens = new();

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
        var token = _creatorTokens.GetOrAdd(creatorId, _ => new CancellationTokenSource());
        _cache.Set(Key(creatorId, range), value, new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(Ttl)
            .AddExpirationToken(new CancellationChangeToken(token.Token)));
    }

    public void Remove(int creatorId)
    {
        if (_creatorTokens.TryRemove(creatorId, out var token))
        {
            token.Cancel();
            token.Dispose();
        }
    }

    private static string Key(int creatorId, string range) => $"dashboard-trends:{creatorId}:{range}";
}
