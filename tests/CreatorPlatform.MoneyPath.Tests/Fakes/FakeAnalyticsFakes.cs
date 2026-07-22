using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.EmailCaptures;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeEmailCaptureRepository : IEmailCaptureRepository
{
    public List<EmailCapture> Added { get; } = [];
    public List<(int CreatorId, int LandingPageId, string Email, DateTimeOffset CapturedAt)> ContactSummaryUpserts { get; } = [];

    /// <summary>When set, forces the next AddAsync call's return value (simulates a duplicate hitting
    /// ON CONFLICT DO NOTHING) instead of inferring it from prior calls.</summary>
    public bool? NextInsertResult { get; set; }

    public Task<bool> AddAsync(EmailCapture capture, CancellationToken ct)
    {
        if (NextInsertResult is { } forced)
        {
            NextInsertResult = null;
            if (forced) Added.Add(capture);
            return Task.FromResult(forced);
        }

        var alreadyExists = Added.Any(c => c.LandingPageId == capture.LandingPageId && c.Email == capture.Email);
        if (alreadyExists)
            return Task.FromResult(false);

        Added.Add(capture);
        return Task.FromResult(true);
    }

    public Task<long> GetCaptureCountAsync(int landingPageId, CancellationToken ct)
        => Task.FromResult((long)Added.Count(c => c.LandingPageId == landingPageId));

    public Task<List<EmailCapture>> ListByLandingPageIdAsync(int landingPageId, CancellationToken ct)
        => Task.FromResult(Added.Where(c => c.LandingPageId == landingPageId).ToList());

    public Task<List<CapturesBucketRow>> GetBucketedCapturesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
        => Task.FromResult(new List<CapturesBucketRow>());

    public Task UpsertContactSummaryAsync(int creatorId, int landingPageId, string email, DateTimeOffset capturedAt, CancellationToken ct)
    {
        ContactSummaryUpserts.Add((creatorId, landingPageId, email, capturedAt));
        return Task.CompletedTask;
    }
}

public sealed class FakeAnalyticsCreatorContextProvider : ICreatorContextProvider
{
    public int? PlanLimit { get; set; }

    public Task<int?> GetActivePlanLimitAsync(int creatorId, string limitKey, CancellationToken ct)
        => Task.FromResult(PlanLimit);
}

public sealed class FakeAnalyticsUnitOfWork : IAnalyticsUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
