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

    public bool TryGet(string creatorSlug, out HomeSummaryDto? value)
    {
        return _cache.TryGetValue(Key(creatorSlug), out value);
    }

    public void Set(string creatorSlug, HomeSummaryDto value)
    {
        _cache.Set(Key(creatorSlug), value, Ttl);
    }

    public void Remove(string creatorSlug)
    {
        _cache.Remove(Key(creatorSlug));
    }

    // Keyed by slug only: a slug maps to exactly one creator with a single owner, so the owner id in the key
    // was redundant. Dropping it lets us invalidate from places that know the slug but not the user id.
    private static string Key(string slug) => $"home-summary:{slug}";
}
