using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.Shared.Application.Analytics;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeEmailCaptureRepository : IEmailCaptureRepository
{
    /// <summary>Emails (normalised) that have an unsubscribe row for the creator.</summary>
    public HashSet<string> UnsubscribedEmails { get; } = [];

    public Task<bool> IsUnsubscribedAsync(int creatorId, string email, CancellationToken ct)
        => Task.FromResult(UnsubscribedEmails.Contains(email));

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

    public Task<CapturesByPeriodRow> GetCaptureCountsByPeriodAsync(int landingPageId, StatsPeriods periods, CancellationToken ct)
    {
        var captures = Added.Where(c => c.LandingPageId == landingPageId).ToList();
        return Task.FromResult(new CapturesByPeriodRow(
            captures.Count(c => c.CapturedAt >= periods.StartOfToday),
            captures.Count(c => c.CapturedAt >= periods.Last7DaysFrom),
            captures.Count(c => c.CapturedAt >= periods.Last30DaysFrom),
            captures.Count));
    }

    public Task<Dictionary<int, int>> GetCaptureCountsAsync(IReadOnlyCollection<int> landingPageIds, CancellationToken ct)
        => Task.FromResult(Added
            .Where(c => landingPageIds.Contains(c.LandingPageId))
            .GroupBy(c => c.LandingPageId)
            .ToDictionary(g => g.Key, g => g.Count()));

    public Task<List<NewestCaptureRow>> ListNewestByLandingPageIdAsync(int landingPageId, int limit, CancellationToken ct)
        => Task.FromResult(Added
            .Where(c => c.LandingPageId == landingPageId)
            .OrderByDescending(c => c.CapturedAt)
            .Take(limit)
            .Select(c => new NewestCaptureRow(c.Email, c.CapturedAt, false))
            .ToList());

    /// <summary>Reports "new contact" for the first upsert of a (creator, email), like the real RETURNING (xmax = 0).</summary>
    public Task<bool> UpsertContactSummaryAsync(int creatorId, int landingPageId, string email, DateTimeOffset capturedAt, CancellationToken ct)
    {
        var isNew = !ContactSummaryUpserts.Any(u => u.CreatorId == creatorId && u.Email == email);
        ContactSummaryUpserts.Add((creatorId, landingPageId, email, capturedAt));
        return Task.FromResult(isNew);
    }
}

public sealed class FakeAnalyticsCreatorContextProvider : ICreatorContextProvider
{
    public int? PlanLimit { get; set; }

    public Task<int?> GetCreatorIdBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
        => Task.FromResult<int?>(null);

    public Task<int?> GetActivePlanLimitAsync(int creatorId, string limitKey, CancellationToken ct)
        => Task.FromResult(PlanLimit);
}

public sealed class FakeAnalyticsUnitOfWork : IAnalyticsUnitOfWork
{
    public List<(int CreatorId, string Email)> ContactLocks { get; } = [];

    public Task AcquireContactLockAsync(int creatorId, string normalizedEmail, CancellationToken ct)
    {
        ContactLocks.Add((creatorId, normalizedEmail));
        return Task.CompletedTask;
    }

    public int SaveChangesCallCount { get; private set; }

    /// <summary>True when the last transaction's operation completed without throwing.</summary>
    public bool? LastTransactionCommitted { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct)
    {
        LastTransactionCommitted = false;
        await operation();
        LastTransactionCommitted = true;
    }
}

/// <summary>Records every delivery call the capture service makes (the real one is covered by integration tests).</summary>
public sealed class FakeLeadMagnetDelivery : ILeadMagnetDelivery
{
    public List<(int CreatorId, int LandingPageId, string Email)> Deliveries { get; } = [];

    public Task DeliverAsync(int creatorId, int landingPageId, string normalizedEmail, DateTimeOffset now, CancellationToken ct)
    {
        Deliveries.Add((creatorId, landingPageId, normalizedEmail));
        return Task.CompletedTask;
    }
}
