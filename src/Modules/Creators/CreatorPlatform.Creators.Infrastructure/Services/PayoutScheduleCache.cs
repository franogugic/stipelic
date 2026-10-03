using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace CreatorPlatform.Creators.Infrastructure.Services;

public sealed class PayoutScheduleCache : IPayoutScheduleCache
{
    // The schedule is read live from Stripe; a minute keeps repeated visits to the payouts screen off the Stripe API
    // while a changed schedule still shows up quickly. Only successful reads are cached, so a Stripe outage is
    // retried on the next request. (Moves to Redis with the other caches in chapter 09.)
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    public PayoutScheduleCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool TryGet(int creatorId, out PayoutScheduleDto? value)
    {
        return _cache.TryGetValue(Key(creatorId), out value);
    }

    public void Set(int creatorId, PayoutScheduleDto value)
    {
        _cache.Set(Key(creatorId), value, Ttl);
    }

    private static string Key(int creatorId) => $"payout-schedule:{creatorId}";
}
