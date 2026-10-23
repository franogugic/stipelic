using CreatorPlatform.Marketing.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeContactsRepository : IContactsRepository
{
    public List<ContactRow> Rows { get; set; } = [];
    public (int CreatorId, string? Search, int? LandingPageId, string? AfterEmail, int Limit)? LastCall { get; private set; }
    public int SearchCallCount { get; private set; }

    public Task<List<ContactRow>> SearchAsync(
        int creatorId, string? search, int? landingPageId, ContactStatusFilter? status, string? afterEmail, int limit,
        CancellationToken ct)
    {
        LastCall = (creatorId, search, landingPageId, afterEmail, limit);
        SearchCallCount++;
        // Keyset semantics of the real query: ordered by email, strictly after the cursor, limit + 1 rows.
        return Task.FromResult(Rows
            .Where(r => afterEmail is null || string.CompareOrdinal(r.Email, afterEmail) > 0)
            .OrderBy(r => r.Email, StringComparer.Ordinal)
            .Take(limit + 1)
            .ToList());
    }

    public Task<ContactStatsCountsRow> GetStatsCountsAsync(int creatorId, DateTimeOffset monthStart, CancellationToken ct)
        => Task.FromResult(new ContactStatsCountsRow(0, 0, 0, 0));

    public Task<List<ContactGrowthRow>> GetGrowthAsync(
        int creatorId, DateTimeOffset windowStart, DateTimeOffset lastMonthStart, DateTimeOffset windowEnd, string timezone,
        CancellationToken ct)
        => Task.FromResult(new List<ContactGrowthRow>());

    /// <summary>What <see cref="DeleteAsync"/> returns — false (the default) models "no such contact".</summary>
    public bool ContactExists { get; set; }
    public (int CreatorId, string Email)? LastDeleteCall { get; private set; }

    public Task<bool> ExistsAsync(int creatorId, string email, CancellationToken ct)
        => Task.FromResult(ContactExists);

    public Task<bool> DeleteAsync(int creatorId, string email, CancellationToken ct)
    {
        LastDeleteCall = (creatorId, email);
        return Task.FromResult(ContactExists);
    }

    public Task<List<ContactSourceCountRow>> GetSourceCountsAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(new List<ContactSourceCountRow>());

    public Task<List<ContactMonthRow>> GetMonthlyAsync(int creatorId, string timezone, CancellationToken ct)
        => Task.FromResult(new List<ContactMonthRow>());
}
