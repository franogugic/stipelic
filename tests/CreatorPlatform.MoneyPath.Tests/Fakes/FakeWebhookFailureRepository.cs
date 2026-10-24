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

    public Task<WebhookFailure?> GetByIdAsync(int id, CancellationToken ct)
        => Task.FromResult(Added.FirstOrDefault(f => f.Id == id));

    public Task<WebhookFailure?> GetOpenHandlerFailureAsync(string provider, string eventId, CancellationToken ct)
        => Task.FromResult(Added.FirstOrDefault(f =>
            f.Provider == provider && f.EventId == eventId && !f.IsResolved && f.Kind == WebhookFailureKind.HandlerFailed));

    public Task<int> ResolveOpenHandlerFailuresAsync(string provider, string eventId, DateTimeOffset resolvedAt, CancellationToken ct)
    {
        var open = Added.Where(f =>
            f.Provider == provider && f.EventId == eventId && !f.IsResolved && f.Kind == WebhookFailureKind.HandlerFailed).ToList();
        open.ForEach(f => f.MarkResolved(resolvedAt));
        return Task.FromResult(open.Count);
    }

    public Task<List<WebhookFailure>> ListAsync(bool? resolved, int limit, CancellationToken ct)
        => Task.FromResult(Added.Where(f => resolved is null || f.IsResolved == resolved).Take(limit).ToList());

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
