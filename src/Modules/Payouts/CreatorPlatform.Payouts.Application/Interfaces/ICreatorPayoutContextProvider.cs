using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Payouts.Application.Interfaces;

public sealed record CreatorPayoutContext(
    int CreatorId,
    Guid CreatorPublicId,
    string Name,
    string Slug,
    CreatorStatus Status,
    PayoutMode PayoutMode,
    Currency Currency,
    bool HasPayoutProfile);

/// <summary>The bank details a payout goes to (shown to the admin in the request notice).</summary>
public sealed record CreatorPayoutBankDetails(string AccountHolderName, string Iban, string BankCountryCode);

public interface ICreatorPayoutContextProvider
{
    /// <summary>The creator's saved payout profile, or null when none is saved.</summary>
    Task<CreatorPayoutBankDetails?> GetBankDetailsAsync(int creatorId, CancellationToken ct);

    Task<CreatorPayoutContext?> GetByPublicIdAsync(Guid creatorPublicId, CancellationToken ct);

    Task<CreatorPayoutContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);
}
