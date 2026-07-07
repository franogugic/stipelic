using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CreatorPlatform.Analytics.Infrastructure.Services;

public sealed class LandingPageTimeSeriesCache : ILandingPageTimeSeriesCache
{
    private readonly IMemoryCache _cache;
    // Shorter TTL than the home summary cache — this endpoint is more interactive (user flips periods).
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);

    public LandingPageTimeSeriesCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryGet(int landingPageId, TimeSeriesPeriod period, out TimeSeriesResponseDto? value)
    {
        return _cache.TryGetValue(Key(landingPageId, period), out value);
    }

    public void Set(int landingPageId, TimeSeriesPeriod period, TimeSeriesResponseDto value)
    {
        _cache.Set(Key(landingPageId, period), value, Ttl);
    }

    private static string Key(int landingPageId, TimeSeriesPeriod period) => $"landingpage-timeseries:{landingPageId}:{period}";
}
