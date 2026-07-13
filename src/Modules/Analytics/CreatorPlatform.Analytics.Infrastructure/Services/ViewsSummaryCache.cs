using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CreatorPlatform.Analytics.Infrastructure.Services;

public sealed class ViewsSummaryCache : IViewsSummaryCache
{
    private readonly IMemoryCache _cache;

    // Page views are recorded on every public visit, far more often than a creator revisits their dashboard,
    // so we deliberately don't invalidate on each recorded view (that would defeat the cache almost entirely).
    // A short TTL bounds staleness instead; landing page create/archive still invalidate explicitly so a
    // newly created page shows up without waiting out the window.
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);

    public ViewsSummaryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryGet(string creatorSlug, out List<LandingPageViewsSummaryDto>? value)
    {
        return _cache.TryGetValue(Key(creatorSlug), out value);
    }

    public void Set(string creatorSlug, List<LandingPageViewsSummaryDto> value)
    {
        _cache.Set(Key(creatorSlug), value, Ttl);
    }

    public void Remove(string creatorSlug)
    {
        _cache.Remove(Key(creatorSlug));
    }

    private static string Key(string slug) => $"views-summary:{slug}";
}
