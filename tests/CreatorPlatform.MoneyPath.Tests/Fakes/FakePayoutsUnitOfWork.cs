using CreatorPlatform.Payouts.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakePayoutsUnitOfWork : IPayoutsUnitOfWork
{
    public int? LastLockedCreatorId { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct) => operation();

    public Task AcquireCreatorPayoutLockAsync(int creatorId, CancellationToken ct)
    {
        LastLockedCreatorId = creatorId;
        return Task.CompletedTask;
    }
}
