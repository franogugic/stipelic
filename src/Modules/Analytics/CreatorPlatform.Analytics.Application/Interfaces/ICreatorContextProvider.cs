namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface ICreatorContextProvider
{
    /// <summary>Resolves the internal creator id for an owner-scoped request. Null if the slug doesn't
    /// exist, isn't owned by this user, or the creator is disabled.</summary>
    Task<int?> GetCreatorIdBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>Plan limit for <paramref name="limitKey"/> from the creator's active subscription. Null
    /// when the creator has no active subscription.</summary>
    Task<int?> GetActivePlanLimitAsync(int creatorId, string limitKey, CancellationToken ct);
}
