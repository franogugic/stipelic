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

    public bool TryGet(string creatorSlug, int ownerUserId, out HomeSummaryDto? value)
    {
        return _cache.TryGetValue(Key(creatorSlug, ownerUserId), out value);
    }

    public void Set(string creatorSlug, int ownerUserId, HomeSummaryDto value)
    {
        _cache.Set(Key(creatorSlug, ownerUserId), value, Ttl);
    }

    private static string Key(string slug, int userId) => $"home-summary:{slug}:{userId}";
}
