using CreatorPlatform.Payouts.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeCreatorPayoutContextProvider : ICreatorPayoutContextProvider
{
    public CreatorPayoutContext? ContextByPublicId { get; set; }
    public CreatorPayoutContext? ContextBySlug { get; set; }
    public CreatorPayoutBankDetails? BankDetails { get; set; }

    public Task<CreatorPayoutBankDetails?> GetBankDetailsAsync(int creatorId, CancellationToken ct)
        => Task.FromResult(BankDetails);

    public Task<CreatorPayoutContext?> GetByPublicIdAsync(Guid creatorPublicId, CancellationToken ct)
        => Task.FromResult(ContextByPublicId);

    public Task<CreatorPayoutContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(ContextBySlug);
}
