using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

/// <summary>Short-lived cache of a Connect account's payout schedule per creator. Read or written only after the
/// caller's ownership of the creator was checked — the key is the internal creator id, never anything the caller
/// supplies. Never throws for an unavailable cache: a read is then a miss, a write does nothing.</summary>
public interface IPayoutScheduleCache
{
    ValueTask<PayoutScheduleDto?> GetAsync(int creatorId, CancellationToken ct);
    Task SetAsync(int creatorId, PayoutScheduleDto value, CancellationToken ct);
}
