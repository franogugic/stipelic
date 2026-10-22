using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Redis;

namespace CreatorPlatform.Orders.Infrastructure.Services;

public sealed class HomeSummaryCache : IHomeSummaryCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly RedisJsonCache _cache;

    public HomeSummaryCache(RedisJsonCache cache)
    {
        _cache = cache;
    }

    public ValueTask<HomeSummaryDto?> GetAsync(int creatorId, CancellationToken ct) =>
        _cache.GetAsync<HomeSummaryDto>(Key(creatorId), ct);

    public Task SetAsync(int creatorId, HomeSummaryDto value, CancellationToken ct) =>
        _cache.SetAsync(Key(creatorId), value, Ttl, ct);

    public Task RemoveAsync(int creatorId, CancellationToken ct) =>
        _cache.RemoveAsync(Key(creatorId), ct);

    // Keyed by the internal creator id, resolved by the ownership check, so a request for someone else's slug
    // can neither read nor overwrite the owner's entry.
    public static string Key(int creatorId) => $"home-summary:{creatorId}";
}
