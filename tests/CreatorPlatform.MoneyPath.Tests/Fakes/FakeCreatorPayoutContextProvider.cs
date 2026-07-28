using CreatorPlatform.Payouts.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeCreatorPayoutContextProvider : ICreatorPayoutContextProvider
{
    public CreatorPayoutContext? ContextByPublicId { get; set; }
    public CreatorPayoutContext? ContextBySlug { get; set; }

    public Task<CreatorPayoutContext?> GetByPublicIdAsync(Guid creatorPublicId, CancellationToken ct)
        => Task.FromResult(ContextByPublicId);

    public Task<CreatorPayoutContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(ContextBySlug);
}
