using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Domain.Payouts;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakePayoutRepository : IPayoutRepository
{
    private int _nextId = 1;

    public List<Payout> Added { get; } = [];
    public Payout? PayoutByPublicId { get; set; }
    public int PendingAmountCents { get; set; }
    public List<PayoutDto> RecentPayouts { get; set; } = [];

    public Task AddAsync(Payout payout, CancellationToken ct)
    {
        typeof(Payout).GetProperty(nameof(Payout.Id))!.SetValue(payout, _nextId++);
        Added.Add(payout);
        PayoutByPublicId = payout;
        return Task.CompletedTask;
    }

    public Task<Payout?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct)
        => Task.FromResult(PayoutByPublicId);

    public Task<int> GetPendingAmountCentsByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(PendingAmountCents);

    public Task<List<PayoutDto>> ListRecentByCreatorIdAsync(int creatorId, int limit, CancellationToken ct)
        => Task.FromResult(RecentPayouts);
}
