using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Redis;

namespace CreatorPlatform.Analytics.Infrastructure.Services;

public sealed class ViewsSummaryCache : IViewsSummaryCache
{
    // Page views are recorded on every public visit, far more often than a creator revisits their dashboard,
    // so we deliberately don't invalidate on each recorded view (that would defeat the cache almost entirely).
    // A short TTL bounds staleness instead; landing page create/archive still invalidate explicitly so a
    // newly created page shows up without waiting out the window.
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);

    private readonly RedisJsonCache _cache;

    public ViewsSummaryCache(RedisJsonCache cache)
    {
        _cache = cache;
    }

    public ValueTask<List<LandingPageViewsSummaryDto>?> GetAsync(int creatorId, CancellationToken ct) =>
        _cache.GetAsync<List<LandingPageViewsSummaryDto>>(Key(creatorId), ct);

    public Task SetAsync(int creatorId, List<LandingPageViewsSummaryDto> value, CancellationToken ct) =>
        _cache.SetAsync(Key(creatorId), value, Ttl, ct);

    public Task RemoveAsync(int creatorId, CancellationToken ct) =>
        _cache.RemoveAsync(Key(creatorId), ct);

    // Keyed by the internal creator id, resolved by the ownership check, so a request for someone else's slug
    // can neither read nor overwrite the owner's entry.
    public static string Key(int creatorId) => $"views-summary:{creatorId}";
}
