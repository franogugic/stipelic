using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Shared.Infrastructure.Redis;

namespace CreatorPlatform.Creators.Infrastructure.Services;

public sealed class PayoutScheduleCache : IPayoutScheduleCache
{
    // The schedule is read live from Stripe; a minute keeps repeated visits to the payouts screen off the Stripe API
    // while a changed schedule still shows up quickly. Only successful reads are cached (the caller's rule), so a
    // Stripe outage is retried on the next request.
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly RedisJsonCache _cache;

    public PayoutScheduleCache(RedisJsonCache cache)
    {
        _cache = cache;
    }

    public ValueTask<PayoutScheduleDto?> GetAsync(int creatorId, CancellationToken ct) =>
        _cache.GetAsync<PayoutScheduleDto>(Key(creatorId), ct);

    public Task SetAsync(int creatorId, PayoutScheduleDto value, CancellationToken ct) =>
        _cache.SetAsync(Key(creatorId), value, Ttl, ct);

    public static string Key(int creatorId) => $"payout-schedule:{creatorId}";
}
