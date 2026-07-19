using System.Reflection;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeMarketingCreatorContextProvider : ICreatorContextProvider
{
    public MarketingCreatorContext? Context { get; set; }
    public int? LandingPageId { get; set; }
    public int? ProductId { get; set; }
    public int? PlanLimit { get; set; }
    public Dictionary<int, Guid> LandingPagePublicIds { get; set; } = [];
    public Dictionary<int, Guid> ProductPublicIds { get; set; } = [];

    public Task<MarketingCreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(Context);

    public Task<int?> ResolveLandingPageIdAsync(int creatorId, Guid landingPagePublicId, CancellationToken ct)
        => Task.FromResult(LandingPageId);

    public Task<int?> ResolveProductIdAsync(int creatorId, Guid productPublicId, CancellationToken ct)
        => Task.FromResult(ProductId);

    public Task<int?> GetActivePlanLimitAsync(int creatorId, string limitKey, CancellationToken ct)
        => Task.FromResult(PlanLimit);

    public Task<Dictionary<int, Guid>> GetLandingPagePublicIdsAsync(IReadOnlyCollection<int> landingPageIds, CancellationToken ct)
        => Task.FromResult(LandingPagePublicIds
            .Where(kv => landingPageIds.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value));

    public Task<Dictionary<int, Guid>> GetProductPublicIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
        => Task.FromResult(ProductPublicIds
            .Where(kv => productIds.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value));
}

public sealed class FakeAudienceService : IAudienceService
{
    public int Count { get; set; }
    public List<string> Emails { get; set; } = [];

    public Task<int> GetAudienceCountAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct)
        => Task.FromResult(Count);

    public Task<List<string>> GetAudienceEmailsAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct)
        => Task.FromResult(Emails);
}

/// <summary>Faithful in-memory reimplementation of the real limit-check semantics (not just a canned
/// return value) — genuinely useful for testing consumers' behavior around the limit boundary.</summary>
public sealed class FakeCreatorUsageService : ICreatorUsageService
{
    public Dictionary<(int CreatorId, string UsageKey), int> Used { get; } = [];
    public List<(int CreatorId, string UsageKey, int Amount, int Limit, UsagePeriod Period)> ConsumeCalls { get; } = [];

    public Task<bool> TryConsumeAsync(int creatorId, string usageKey, int amount, int limit, UsagePeriod period, CancellationToken ct)
    {
        ConsumeCalls.Add((creatorId, usageKey, amount, limit, period));
        var key = (creatorId, usageKey);
        var current = Used.GetValueOrDefault(key);

        if (limit >= 0 && current + amount > limit)
            return Task.FromResult(false);

        Used[key] = current + amount;
        return Task.FromResult(true);
    }

    public Task<int> GetUsedAsync(int creatorId, string usageKey, UsagePeriod period, CancellationToken ct)
        => Task.FromResult(Used.GetValueOrDefault((creatorId, usageKey)));
}

public sealed class FakeCampaignRepository : ICampaignRepository
{
    public List<Campaign> Campaigns { get; } = [];

    public Task AddAsync(Campaign campaign, CancellationToken ct)
    {
        Campaigns.Add(campaign);
        return Task.CompletedTask;
    }

    public Task<Campaign?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct)
        => Task.FromResult(Campaigns.FirstOrDefault(c => c.PublicId == publicId));

    public Task<Campaign?> GetByPublicIdAsync(Guid publicId, CancellationToken ct)
        => Task.FromResult(Campaigns.FirstOrDefault(c => c.PublicId == publicId));

    public Task<List<Campaign>> GetRecentByCreatorIdAsync(int creatorId, int take, CancellationToken ct)
        => Task.FromResult(Campaigns
            .Where(c => c.CreatorId == creatorId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .ToList());

    public void Remove(Campaign campaign)
        => Campaigns.Remove(campaign);
}

/// <summary>Assigns a DB-generated-looking Id to each recipient as soon as it's added — approximating the
/// real repository's post-flush behavior (the send pipeline reads recipient.Id right after the first
/// SaveChanges) without needing a real database or EF InMemory.</summary>
public sealed class FakeCampaignRecipientRepository : ICampaignRecipientRepository
{
    private static readonly PropertyInfo IdProperty =
        typeof(CampaignRecipient).GetProperty(nameof(CampaignRecipient.Id))!;

    private int _nextId = 1;

    public List<CampaignRecipient> Recipients { get; } = [];

    public Task AddRangeAsync(IEnumerable<CampaignRecipient> recipients, CancellationToken ct)
    {
        foreach (var recipient in recipients)
        {
            IdProperty.SetValue(recipient, _nextId++);
            Recipients.Add(recipient);
        }

        return Task.CompletedTask;
    }
}

public sealed class FakeMarketingUnitOfWork : IMarketingUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }
    public bool TransactionCommitted { get; private set; }
    public int? LockedCreatorId { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct)
    {
        await operation();
        TransactionCommitted = true;
    }

    public Task AcquireCreatorCampaignLockAsync(int creatorId, CancellationToken ct)
    {
        LockedCreatorId = creatorId;
        return Task.CompletedTask;
    }
}

public sealed class FakeCampaignProgressProvider : ICampaignProgressProvider
{
    public Dictionary<Guid, CampaignProgressDto> Progress { get; set; } = [];

    public Task<Dictionary<Guid, CampaignProgressDto>> GetProgressAsync(
        IReadOnlyCollection<Guid> campaignPublicIds, CancellationToken ct)
        => Task.FromResult(campaignPublicIds.ToDictionary(
            id => id,
            id => Progress.GetValueOrDefault(id, new CampaignProgressDto(0, 0))));
}
