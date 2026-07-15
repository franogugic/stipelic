using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorRepository
{
    Task<Creator?> GetByOwnerUserIdAsync(int ownerUserId, CancellationToken ct);

    /// <summary>Ownership check by slug + owner — same pattern as <see cref="ICreatorSettingsRepository"/>.</summary>
    Task<Creator?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>Tracked variant of <see cref="GetBySlugForOwnerAsync"/> — for flows that may create a new
    /// dependent row (e.g. a first-time payout profile) referencing this Creator via navigation.</summary>
    Task<Creator?> GetForUpdateBySlugAndOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

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
