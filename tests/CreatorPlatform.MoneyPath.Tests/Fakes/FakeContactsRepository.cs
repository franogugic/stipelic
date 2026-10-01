using CreatorPlatform.Marketing.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeContactsRepository : IContactsRepository
{
    public List<ContactRow> Rows { get; set; } = [];
    public (int CreatorId, string? Search, int? LandingPageId, string? AfterEmail, int Limit)? LastCall { get; private set; }

    public Task<List<ContactRow>> SearchAsync(
        int creatorId, string? search, int? landingPageId, string? afterEmail, int limit, CancellationToken ct)
    {
        LastCall = (creatorId, search, landingPageId, afterEmail, limit);
        return Task.FromResult(Rows.Take(limit + 1).ToList());
    }

    public Task<ContactStatsCountsRow> GetStatsCountsAsync(int creatorId, DateTimeOffset monthStart, CancellationToken ct)
        => Task.FromResult(new ContactStatsCountsRow(0, 0, 0, 0));

    public Task<List<ContactGrowthRow>> GetGrowthAsync(
        int creatorId, DateTimeOffset windowStart, DateTimeOffset lastMonthStart, CancellationToken ct)
        => Task.FromResult(new List<ContactGrowthRow>());

    public Task<List<ContactSourceCountRow>> GetSourceCountsAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(new List<ContactSourceCountRow>());
}
