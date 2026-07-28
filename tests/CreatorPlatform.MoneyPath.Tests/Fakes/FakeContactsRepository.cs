using CreatorPlatform.Marketing.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeContactsRepository : IContactsRepository
{
    public List<ContactRow> Rows { get; set; } = [];
    public (int CreatorId, string? Search, string? AfterEmail, int Limit)? LastCall { get; private set; }

    public Task<List<ContactRow>> SearchAsync(int creatorId, string? search, string? afterEmail, int limit, CancellationToken ct)
    {
        LastCall = (creatorId, search, afterEmail, limit);
        return Task.FromResult(Rows.Take(limit + 1).ToList());
    }
}
