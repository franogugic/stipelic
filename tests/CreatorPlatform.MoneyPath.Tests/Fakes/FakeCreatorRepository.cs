using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

/// <summary>
/// Single in-memory fake covering every ICreatorRepository member — reused across the Connect
/// onboarding, account.updated webhook and CreatorService.CreateAsync test fixtures. Behavior for a
/// given scenario is wired up by setting the public fields before invoking the service under test.
/// </summary>
public sealed class FakeCreatorRepository : ICreatorRepository
{
    private readonly List<string> _callLog;

    public FakeCreatorRepository(List<string>? callLog = null)
    {
        _callLog = callLog ?? [];
    }

    public List<string> CallLog => _callLog;

    /// <summary>Returned by GetByOwnerUserId* lookups.</summary>
    public Creator? CreatorByOwner { get; set; }

    /// <summary>Returned alongside CreatorByOwner by GetByOwnerUserIdWithPayoutProfileAsync.</summary>
    public bool HasPayoutProfile { get; set; }

    /// <summary>Returned by GetByStripeConnectAccountIdForUpdateAsync.</summary>
    public Creator? CreatorByStripeConnectAccountId { get; set; }

    public bool SlugExists { get; set; }
    public bool ExistsByOwner { get; set; }

    public List<Creator> Added { get; } = [];

    public Task<Creator?> GetByOwnerUserIdAsync(int ownerUserId, CancellationToken ct)
    {
        return Task.FromResult(CreatorByOwner);
    }

    public Task<Creator?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return Task.FromResult(CreatorByOwner);
    }

    public Task<Creator?> GetForUpdateBySlugAndOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return Task.FromResult(CreatorByOwner);
    }

    public Task<Creator?> GetByIdForUpdateAsync(int id, CancellationToken ct)
    {
        return Task.FromResult(CreatorByOwner);
    }

    public Task<Creator?> GetByOwnerUserIdForUpdateAsync(int ownerUserId, CancellationToken ct)
    {
        return Task.FromResult(CreatorByOwner);
    }

    public Task<(Creator? Creator, bool HasPayoutProfile)> GetByOwnerUserIdWithPayoutProfileAsync(int ownerUserId, CancellationToken ct)
    {
        return Task.FromResult((CreatorByOwner, HasPayoutProfile));
    }

    public Task<Creator?> GetByStripeConnectAccountIdForUpdateAsync(string stripeConnectAccountId, CancellationToken ct)
    {
        return Task.FromResult(CreatorByStripeConnectAccountId);
    }

    public Task<bool> ExistsByOwnerUserIdAsync(int ownerUserId, CancellationToken ct)
    {
        return Task.FromResult(ExistsByOwner);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct)
    {
        return Task.FromResult(SlugExists);
    }

    public Task AddAsync(Creator creator, CancellationToken ct)
    {
        Added.Add(creator);
        return Task.CompletedTask;
    }

    public Task<bool> DisableByOwnerUserIdAsync(int ownerUserId, DateTimeOffset disabledAt, CancellationToken ct)
    {
        return Task.FromResult(false);
    }
}
