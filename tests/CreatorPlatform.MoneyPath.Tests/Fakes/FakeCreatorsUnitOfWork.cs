using CreatorPlatform.Creators.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeCreatorsUnitOfWork : ICreatorsUnitOfWork
{
    private readonly List<string> _callLog;

    public FakeCreatorsUnitOfWork(List<string>? callLog = null)
    {
        _callLog = callLog ?? [];
    }

    public int SaveChangesCallCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        _callLog.Add("SaveChanges");
        return Task.CompletedTask;
    }

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct)
    {
        return operation();
    }
}
