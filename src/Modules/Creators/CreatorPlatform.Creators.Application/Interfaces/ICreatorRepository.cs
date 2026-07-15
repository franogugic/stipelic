using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorRepository
{
    Task<Creator?> GetByOwnerUserIdAsync(int ownerUserId, CancellationToken ct);

    Task<Creator?> GetByIdForUpdateAsync(int id, CancellationToken ct);

    Task<Creator?> GetByOwnerUserIdForUpdateAsync(int ownerUserId, CancellationToken ct);

    /// <summary>Same scope as <see cref="GetByOwnerUserIdAsync"/>, but also reports whether a payout
    /// profile row exists — one query (correlated EXISTS), no extra roundtrip.</summary>
    Task<(Creator? Creator, bool HasPayoutProfile)> GetByOwnerUserIdWithPayoutProfileAsync(int ownerUserId, CancellationToken ct);

    Task<Creator?> GetByStripeConnectAccountIdForUpdateAsync(string stripeConnectAccountId, CancellationToken ct);

    Task<bool> ExistsByOwnerUserIdAsync(int ownerUserId, CancellationToken ct);

    Task<bool> SlugExistsAsync(string slug, CancellationToken ct);

    Task AddAsync(Creator creator, CancellationToken ct);

    Task<bool> DisableByOwnerUserIdAsync(int ownerUserId, DateTimeOffset disabledAt, CancellationToken ct);
}
