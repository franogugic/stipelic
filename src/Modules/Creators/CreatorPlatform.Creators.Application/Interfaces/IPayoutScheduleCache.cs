using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Interfaces;

/// <summary>Short-lived cache of a Connect account's payout schedule per creator. Read or written only after the
/// caller's ownership of the creator was checked — the key is the internal creator id, never anything the caller
/// supplies.</summary>
public interface IPayoutScheduleCache
{
    bool TryGet(int creatorId, out PayoutScheduleDto? value);
    void Set(int creatorId, PayoutScheduleDto value);
}
