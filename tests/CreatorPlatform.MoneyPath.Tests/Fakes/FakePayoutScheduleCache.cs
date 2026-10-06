using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakePayoutScheduleCache : IPayoutScheduleCache
{
    private readonly Dictionary<int, PayoutScheduleDto> _entries = [];

    public bool TryGet(int creatorId, out PayoutScheduleDto? value)
    {
        var found = _entries.TryGetValue(creatorId, out var entry);
        value = entry;
        return found;
    }

    public void Set(int creatorId, PayoutScheduleDto value) => _entries[creatorId] = value;
}
