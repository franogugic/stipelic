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
    public List<AdminPayoutQueueItemDto> QueueItems { get; set; } = [];

    /// <summary>Overrides <see cref="HasPendingPayoutAsync"/>'s result when set; otherwise it's derived
    /// from <see cref="Added"/> so a real Pending payout added earlier in the same test is honored
    /// without every test having to wire this up by hand.</summary>
    public bool? HasPendingPayoutOverride { get; set; }

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

    public Task<bool> HasPendingPayoutAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(HasPendingPayoutOverride ?? Added.Any(p => p.CreatorId == creatorId && p.Status == PayoutStatus.Pending));

    public Task<List<AdminPayoutQueueItemDto>> ListForQueueAsync(PayoutStatus? status, int limit, CancellationToken ct)
        => Task.FromResult(status is null ? QueueItems : QueueItems.Where(q => q.Status == status.Value.ToString()).ToList());
}
