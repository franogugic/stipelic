using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Domain;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeWebhookFailureRepository : IWebhookFailureRepository
{
    public List<WebhookFailure> Added { get; } = [];
    public int SaveChangesCallCount { get; private set; }

    public Task AddAsync(WebhookFailure failure, CancellationToken ct)
    {
        Added.Add(failure);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
