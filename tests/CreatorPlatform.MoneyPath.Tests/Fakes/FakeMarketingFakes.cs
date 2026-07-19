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

    public Task<MarketingCreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(Context);

    public Task<int?> ResolveLandingPageIdAsync(int creatorId, Guid landingPagePublicId, CancellationToken ct)
        => Task.FromResult(LandingPageId);

    public Task<int?> ResolveProductIdAsync(int creatorId, Guid productPublicId, CancellationToken ct)
        => Task.FromResult(ProductId);

    public Task<int?> GetActivePlanLimitAsync(int creatorId, string limitKey, CancellationToken ct)
        => Task.FromResult(PlanLimit);
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
