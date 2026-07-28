using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeCreatorPayoutProfileRepository : ICreatorPayoutProfileRepository
{
    public CreatorPayoutProfile? ProfileByCreatorId { get; set; }

    public List<CreatorPayoutProfile> Added { get; } = [];

    public Task AddAsync(CreatorPayoutProfile profile, CancellationToken ct)
    {
        Added.Add(profile);
        ProfileByCreatorId = profile;
        return Task.CompletedTask;
    }

    public Task<CreatorPayoutProfile?> GetByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(ProfileByCreatorId);

    public Task<CreatorPayoutProfile?> GetForUpdateByCreatorIdAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(ProfileByCreatorId);
}
