using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeCreatorMemberRepository : ICreatorMemberRepository
{
    public List<CreatorMember> Added { get; } = [];

    public Task AddAsync(CreatorMember member, CancellationToken ct)
    {
        Added.Add(member);
        return Task.CompletedTask;
    }
}

public sealed class FakeCreatorPlanRepository : ICreatorPlanRepository
{
    /// <summary>Keyed by plan code — set up whichever codes a given test needs.</summary>
    public Dictionary<string, CreatorPlan> PlansByCode { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<List<CreatorPlan>> ListActiveAsync(CancellationToken ct)
    {
        return Task.FromResult(PlansByCode.Values.ToList());
    }

    public Task<CreatorPlan?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return Task.FromResult(PlansByCode.GetValueOrDefault(code));
    }

    public Task<CreatorPlan?> GetByStripePriceIdAsync(string stripePriceId, CancellationToken ct)
    {
        return Task.FromResult(PlansByCode.Values.FirstOrDefault(p => p.StripePriceId == stripePriceId));
    }
}

public sealed class FakeCreatorSettingsRepository : ICreatorSettingsRepository
{
    public List<CreatorSettings> Added { get; } = [];

    public Task AddAsync(CreatorSettings settings, CancellationToken ct)
    {
        Added.Add(settings);
        return Task.CompletedTask;
    }

    public Task<CreatorSettings?> GetByCreatorSlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return Task.FromResult<CreatorSettings?>(null);
    }

    public Task<CreatorSettings?> GetForUpdateBySlugAndOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return Task.FromResult<CreatorSettings?>(null);
    }
}

public sealed class FakeCreatorSubscriptionRepository : ICreatorSubscriptionRepository
{
    public List<CreatorSubscription> Added { get; } = [];

    public Task AddAsync(CreatorSubscription subscription, CancellationToken ct)
    {
        Added.Add(subscription);
        return Task.CompletedTask;
    }

    public Task<CreatorSubscription?> GetCurrentByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return Task.FromResult<CreatorSubscription?>(null);
    }

    public Task<CreatorSubscription?> GetByIdForUpdateAsync(int id, CancellationToken ct)
    {
        return Task.FromResult<CreatorSubscription?>(null);
    }

    public Task<CreatorSubscription?> GetByProviderSubscriptionIdForUpdateAsync(string providerSubscriptionId, CancellationToken ct)
    {
        return Task.FromResult<CreatorSubscription?>(null);
    }
}

// The following three are not expected to be exercised by CreatorService.CreateAsync (checkout only
// happens later, via StartSubscriptionCheckoutAsync) — they throw if accidentally invoked, which is a
// stronger assertion than the test scenario would otherwise make explicit.

public sealed class FakeSubscriptionCheckoutSessionService : ISubscriptionCheckoutSessionService
{
    public Task<SubscriptionCheckoutSessionDto> CreateAsync(
        string stripePriceId, string idempotencyKey, IReadOnlyDictionary<string, string> metadata, CancellationToken ct)
    {
        throw new InvalidOperationException("Not expected to be called in this scenario.");
    }
}

public sealed class FakeSubscriptionCancellationService : ISubscriptionCancellationService
{
    public Task CancelAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct)
    {
        throw new InvalidOperationException("Not expected to be called in this scenario.");
    }
}

public sealed class FakeBillingPortalService : IBillingPortalService
{
    public Task<string> CreateSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct)
    {
        throw new InvalidOperationException("Not expected to be called in this scenario.");
    }
}
