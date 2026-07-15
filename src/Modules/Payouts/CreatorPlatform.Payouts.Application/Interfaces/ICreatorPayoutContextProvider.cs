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

public interface ICreatorPayoutContextProvider
{
    Task<CreatorPayoutContext?> GetByPublicIdAsync(Guid creatorPublicId, CancellationToken ct);

    Task<CreatorPayoutContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);
}
