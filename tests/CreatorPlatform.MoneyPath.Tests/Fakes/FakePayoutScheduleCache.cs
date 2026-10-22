using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakePayoutScheduleCache : IPayoutScheduleCache
{
    private readonly Dictionary<int, PayoutScheduleDto> _entries = [];

    public ValueTask<PayoutScheduleDto?> GetAsync(int creatorId, CancellationToken ct) =>
        ValueTask.FromResult(_entries.GetValueOrDefault(creatorId));

    public Task SetAsync(int creatorId, PayoutScheduleDto value, CancellationToken ct)
    {
        _entries[creatorId] = value;
        return Task.CompletedTask;
    }
}
